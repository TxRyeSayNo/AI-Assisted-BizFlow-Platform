using System.Text.Json;
using System.Text.RegularExpressions;
using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.AI;

public sealed class AiTaskDecompositionService(
    ITenantContext tenantContext,
    IResourceAuthorizer authorizer,
    IAiContextReader contextReader,
    ITaskDecompositionStore store,
    TimeProvider clock,
    IAiModelClient? modelClient = null) : IAiTaskDecompositionService
{
    private Guid GetRequiredTenantId() =>
        tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "An active tenant context is required.");

    private Guid GetRequiredUserId() =>
        tenantContext.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "An authenticated user is required.");

    public async Task<TaskDecompositionPreviewResult> PreviewAsync(
        TaskDecompositionPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        _ = GetRequiredUserId();

        if (string.IsNullOrWhiteSpace(command.Prompt))
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task description cannot be empty.");
        }

        var prompt = command.Prompt.Trim();
        if (prompt.Length > 4000)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task prompt cannot exceed 4000 characters.");
        }

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);
        var proposals = new List<TaskDecompositionProposalDto>();

        if (modelClient?.IsConfigured == true)
        {
            try
            {
                var deptSummary = string.Join(", ", context.Departments.Select(d => $"{d.DepartmentId}:{d.Name}"));
                var sysInstructions = $"""
                    You are BizFlow AI Project Manager. Decompose the user's work instruction into structured task items.
                    Each item must be either:
                    - "SupportRequest": cross-department assistance, service request, IT help, equipment, or approvals.
                    - "Assignment": direct execution work, drafting, internal tasks, deliverables, or reviews.

                    Available departments in tenant: [{deptSummary}].
                    Match each task to the most appropriate departmentId and departmentName.

                    Return ONLY a JSON array of objects with:
                    - "title": concise task title (string, max 100 chars)
                    - "description": clear instructions and deliverables (string)
                    - "type": "SupportRequest" or "Assignment"
                    - "departmentId": string UUID from available departments or null
                    - "departmentName": string department name or null
                    - "priority": "LOW", "MEDIUM", or "HIGH"
                    - "checklistItems": array of short step-by-step strings
                    - "reasoning": brief explanation of why this task is structured this way
                    """;

                var aiResp = await modelClient.CompleteAsync(prompt, sysInstructions, cancellationToken);
                using var doc = JsonDocument.Parse(aiResp.Content);
                var rootArray = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement
                    : (doc.RootElement.TryGetProperty("proposals", out var pArray) && pArray.ValueKind == JsonValueKind.Array
                        ? pArray
                        : (doc.RootElement.TryGetProperty("tasks", out var tArray) && tArray.ValueKind == JsonValueKind.Array
                            ? tArray
                            : (doc.RootElement.TryGetProperty("items", out var iArray) && iArray.ValueKind == JsonValueKind.Array
                                ? iArray
                                : default)));

                if (rootArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in rootArray.EnumerateArray())
                    {
                        var title = elem.TryGetProperty("title", out var tp) ? tp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        var desc = elem.TryGetProperty("description", out var dp) ? dp.GetString() : "";
                        var type = elem.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "Assignment";
                        if (type != "SupportRequest" && type != "Assignment") type = "Assignment";

                        var priority = elem.TryGetProperty("priority", out var priProp) ? priProp.GetString()?.ToUpperInvariant() : "MEDIUM";
                        if (priority != "LOW" && priority != "HIGH") priority = "MEDIUM";

                        Guid? deptId = null;
                        string? deptName = null;
                        if (elem.TryGetProperty("departmentId", out var didProp) && Guid.TryParse(didProp.GetString(), out var parsedDid))
                        {
                            var matched = context.Departments.FirstOrDefault(d => d.DepartmentId == parsedDid);
                            if (matched != null)
                            {
                                deptId = matched.DepartmentId;
                                deptName = matched.Name;
                            }
                        }

                        if (deptId == null && elem.TryGetProperty("departmentName", out var dnameProp))
                        {
                            var nameStr = dnameProp.GetString();
                            var matched = context.Departments.FirstOrDefault(d => d.Name.Contains(nameStr ?? "", StringComparison.OrdinalIgnoreCase));
                            if (matched != null)
                            {
                                deptId = matched.DepartmentId;
                                deptName = matched.Name;
                            }
                        }

                        var checklist = new List<string>();
                        if (elem.TryGetProperty("checklistItems", out var clProp) && clProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var clItem in clProp.EnumerateArray())
                            {
                                var s = clItem.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) checklist.Add(s.Trim());
                            }
                        }

                        var reasoning = elem.TryGetProperty("reasoning", out var rp) ? rp.GetString() : null;

                        proposals.Add(new TaskDecompositionProposalDto(
                            Title: title.Trim(),
                            Description: desc ?? "",
                            Type: type,
                            DepartmentId: deptId,
                            DepartmentName: deptName ?? (context.Departments.FirstOrDefault()?.Name ?? "General"),
                            Priority: priority ?? "MEDIUM",
                            ChecklistItems: checklist,
                            Reasoning: reasoning));
                    }
                }
            }
            catch
            {
                // Fall back to deterministic rule-based decomposition
            }
        }

        if (proposals.Count == 0)
        {
            proposals.AddRange(DecomposeDeterministically(prompt, context));
        }

        // BR-028 Invariant: Pure preview, absolutely NO database records created or modified.
        return new TaskDecompositionPreviewResult(prompt, proposals);
    }

    public async Task<ConfirmTaskDecompositionResponse> ConfirmAsync(
        ConfirmTaskDecompositionCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        if (command.Items == null || command.Items.Count == 0)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "At least one task item must be selected for confirmation.");
        }

        if (command.Items.Count > 50)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Cannot confirm more than 50 items in a single batch.");
        }

        // Validate items and verify required permissions
        var hasAssignment = command.Items.Any(i => string.Equals(i.Type, "Assignment", StringComparison.OrdinalIgnoreCase));
        var hasSupportRequest = command.Items.Any(i => string.Equals(i.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase));

        if (hasAssignment)
        {
            await authorizer.AuthorizeAsync("tasks.create", new(tenantId), cancellationToken: cancellationToken);
        }

        if (hasSupportRequest)
        {
            await authorizer.AuthorizeAsync("requests.create", new(tenantId), cancellationToken: cancellationToken);
        }

        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300)
            {
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task title must be between 1 and 300 characters.");
            }

            if (!string.Equals(item.Type, "Assignment", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task type must be either 'Assignment' or 'SupportRequest'.");
            }
        }

        var now = clock.GetUtcNow();
        var created = await store.PersistDecompositionBatchAsync(tenantId, userId, command.Items, now, cancellationToken);

        return new ConfirmTaskDecompositionResponse(created.Count, created);
    }

    private static IReadOnlyList<TaskDecompositionProposalDto> DecomposeDeterministically(
        string prompt,
        AiTenantContextData context)
    {
        var proposals = new List<TaskDecompositionProposalDto>();
        var lines = prompt.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .ToList();

        if (lines.Count <= 1)
        {
            // Split by bullet markers or numbering
            lines = Regex.Split(prompt, @"(?:\d+\.|\-|\*|\•)\s+")
                .Select(l => l.Trim())
                .Where(l => l.Length > 3)
                .ToList();
        }

        if (lines.Count == 0) lines.Add(prompt);

        foreach (var line in lines)
        {
            var isSupport = Regex.IsMatch(line, @"\b(hỗ trợ|request|yêu cầu|nhờ|cấp|sửa|it|hr)\b", RegexOptions.IgnoreCase);
            var type = isSupport ? "SupportRequest" : "Assignment";

            // Find matching department
            var matchedDept = context.Departments.FirstOrDefault(d =>
                line.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                (d.Code != null && line.Contains(d.Code, StringComparison.OrdinalIgnoreCase)));

            var priority = Regex.IsMatch(line, @"\b(khẩn|gấp|urgent|asap|ngay)\b", RegexOptions.IgnoreCase) ? "HIGH" : "MEDIUM";
            var title = line.Length > 80 ? line[..80].Trim() + "…" : line;

            proposals.Add(new TaskDecompositionProposalDto(
                Title: title,
                Description: line,
                Type: type,
                DepartmentId: matchedDept?.DepartmentId ?? context.Departments.FirstOrDefault()?.DepartmentId,
                DepartmentName: matchedDept?.Name ?? (context.Departments.FirstOrDefault()?.Name ?? "General"),
                Priority: priority,
                ChecklistItems: [$"Bước 1: Tiếp nhận và làm rõ {title}", $"Bước 2: Triển khai nội dung {title}", "Bước 3: Bàn giao và nghiệm thu"],
                Reasoning: $"Phân loại dựa trên mục tiêu: {(isSupport ? "Hỗ trợ dịch vụ" : "Giao việc thực hiện")}"));
        }

        return proposals;
    }
}
