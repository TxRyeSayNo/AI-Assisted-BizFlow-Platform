using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Common;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiBranchingService(
    ITenantContext tenantContext,
    IResourceAuthorizer authorizer,
    IAiContextReader contextReader,
    IAiStore store,
    IAiBranchingStore branchingStore,
    IAiConfirmationTokenService confirmationTokenService,
    TimeProvider clock,
    IAiModelClient? modelClient = null) : IAiBranchingService
{
    private const string BranchingToolName = "execute_branching_plan";

    private Guid GetRequiredTenantId() =>
        tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "An active tenant context is required.");

    private Guid GetRequiredUserId() =>
        tenantContext.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "An authenticated user is required.");

    public async Task<BranchingPlanResult> PlanBranchingAsync(
        BranchPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.007", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title) && string.IsNullOrWhiteSpace(command.Description))
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Title or description is required for branching analysis.");
        }

        var title = (command.Title ?? "").Trim();
        var desc = (command.Description ?? "").Trim();
        var combinedText = $"{title}\n{desc}".Trim();
        if (combinedText.Length > 8000)
        {
            combinedText = combinedText[..8000];
        }

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);
        var planId = Guid.NewGuid();
        var branches = new List<BranchPlanNode>();
        var overallStrategy = "Phân nhánh đa luồng theo phòng ban và giai đoạn thực thi phụ thuộc (DAG).";
        decimal confidence = 0.88m;

        if (modelClient?.IsConfigured == true)
        {
            try
            {
                var deptsSummary = string.Join(", ", context.Departments.Select(d => $"{d.DepartmentId}:{d.Name}"));
                var servicesSummary = string.Join(", ", context.Services.Select(s => $"{s.ServiceId}:{s.Name} (Categories: {string.Join(";", s.Categories.Select(c => $"{c.CategoryId}:{c.Name}"))})"));

                var sysInstructions = $"""
                    You are BizFlow AI Workflow & Branching Architect.
                    Your goal is to decompose a complex business request or project into a multi-branch execution tree (Directed Acyclic Graph).
                    Determine the right branches, classifying each as:
                    - "Request": Cross-department requests, service tickets, external approvals, IT provision, HR requests.
                    - "Task": Direct executable work, internal deliverables, engineering, drafting, inspection.

                    Available Departments: [{deptsSummary}]
                    Available Services: [{servicesSummary}]

                    Return a JSON object with:
                    - "strategy": concise summary of the branching strategy
                    - "confidence": decimal between 0.70 and 0.99
                    - "branches": array of branch objects:
                      - "branchIndex": integer (0, 1, 2...)
                      - "branchType": "Request" or "Task"
                      - "title": concise title (string, max 120 chars)
                      - "description": clear instructions and deliverables (string)
                      - "departmentId": string UUID matching an available department or null
                      - "departmentName": string department name
                      - "serviceId": string UUID matching an available service or null
                      - "serviceName": string service name or null
                      - "categoryId": string UUID matching an available category or null
                      - "categoryName": string category name or null
                      - "priority": "Low", "Medium", "High", or "Critical"
                      - "suggestedDurationDays": integer days (1 to 30)
                      - "executionPhase": integer phase (1 = Phase 1/Immediate Parallel, 2 = Phase 2/Dependent, 3 = Phase 3/Closing)
                      - "dependsOnBranchIndexes": array of integers indicating prerequisite branch indices
                      - "checklist": array of 2-4 checklist string steps
                      - "reasoning": brief explanation of why this branch was created
                    Return ONLY valid JSON.
                    """;

                var aiResp = await modelClient.CompleteAsync(combinedText, sysInstructions, cancellationToken);
                using var doc = JsonDocument.Parse(aiResp.Content);
                var root = doc.RootElement;

                if (root.TryGetProperty("strategy", out var stProp))
                {
                    overallStrategy = stProp.GetString() ?? overallStrategy;
                }

                if (root.TryGetProperty("confidence", out var confProp) && confProp.TryGetDecimal(out var parsedConf))
                {
                    confidence = Math.Clamp(parsedConf, 0.50m, 0.99m);
                }

                if (root.TryGetProperty("branches", out var branchesArray) && branchesArray.ValueKind == JsonValueKind.Array)
                {
                    var idx = 0;
                    foreach (var elem in branchesArray.EnumerateArray())
                    {
                        var bTitle = elem.TryGetProperty("title", out var btp) ? btp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(bTitle)) continue;

                        var bType = elem.TryGetProperty("branchType", out var btyp) ? btyp.GetString() : "Task";
                        if (bType != "Request" && bType != "Task") bType = "Task";

                        var bDesc = elem.TryGetProperty("description", out var bdp) ? bdp.GetString() ?? "" : "";
                        var priority = elem.TryGetProperty("priority", out var prp) ? prp.GetString() ?? "Medium" : "Medium";
                        var days = elem.TryGetProperty("suggestedDurationDays", out var sdp) && sdp.TryGetInt32(out var d) ? d : 3;
                        var phase = elem.TryGetProperty("executionPhase", out var epp) && epp.TryGetInt32(out var p) ? p : 1;
                        var reasoning = elem.TryGetProperty("reasoning", out var rnp) ? rnp.GetString() ?? "" : "";

                        Guid? deptId = null;
                        string? deptName = null;
                        if (elem.TryGetProperty("departmentId", out var didp) && Guid.TryParse(didp.GetString(), out var parsedDid))
                        {
                            var dMatch = context.Departments.FirstOrDefault(dep => dep.DepartmentId == parsedDid);
                            deptId = dMatch?.DepartmentId;
                            deptName = dMatch?.Name;
                        }

                        Guid? srvId = null;
                        string? srvName = null;
                        Guid? catId = null;
                        string? catName = null;
                        if (elem.TryGetProperty("serviceId", out var sidp) && Guid.TryParse(sidp.GetString(), out var parsedSid))
                        {
                            var sMatch = context.Services.FirstOrDefault(s => s.ServiceId == parsedSid);
                            if (sMatch != null)
                            {
                                srvId = sMatch.ServiceId;
                                srvName = sMatch.Name;
                                if (elem.TryGetProperty("categoryId", out var cidp) && Guid.TryParse(cidp.GetString(), out var parsedCid))
                                {
                                    var cMatch = sMatch.Categories.FirstOrDefault(c => c.CategoryId == parsedCid);
                                    catId = cMatch?.CategoryId;
                                    catName = cMatch?.Name;
                                }
                                else if (sMatch.Categories.Count > 0)
                                {
                                    catId = sMatch.Categories[0].CategoryId;
                                    catName = sMatch.Categories[0].Name;
                                }
                            }
                        }

                        var deps = new List<int>();
                        if (elem.TryGetProperty("dependsOnBranchIndexes", out var depArr) && depArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var dElem in depArr.EnumerateArray())
                            {
                                if (dElem.TryGetInt32(out var di) && di < idx && di >= 0)
                                    deps.Add(di);
                            }
                        }

                        var checklist = new List<string>();
                        if (elem.TryGetProperty("checklist", out var chkArr) && chkArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cElem in chkArr.EnumerateArray())
                            {
                                if (cElem.GetString() is { } str && !string.IsNullOrWhiteSpace(str))
                                    checklist.Add(str.Trim());
                            }
                        }
                        if (checklist.Count == 0)
                        {
                            checklist.Add($"Làm rõ yêu cầu cho {bTitle}");
                            checklist.Add($"Thực hiện và kiểm thử {bTitle}");
                            checklist.Add("Nghiệm thu và xác nhận hoàn tất");
                        }

                        branches.Add(new BranchPlanNode(
                            BranchIndex: idx,
                            BranchType: bType,
                            Title: bTitle,
                            Description: bDesc,
                            DepartmentId: deptId,
                            DepartmentName: deptName,
                            ServiceId: srvId,
                            ServiceName: srvName,
                            CategoryId: catId,
                            CategoryName: catName,
                            Priority: priority,
                            SuggestedDurationDays: days,
                            ExecutionPhase: phase,
                            DependsOnBranchIndexes: deps,
                            Checklist: checklist,
                            Reasoning: reasoning));

                        idx++;
                    }
                }
            }
            catch
            {
                // Fall through to deterministic heuristic fallback
            }
        }

        if (branches.Count == 0)
        {
            branches = BuildDeterministicBranches(combinedText, context);
        }

        var totalDays = branches.Count > 0 ? branches.GroupBy(b => b.ExecutionPhase).Sum(g => g.Max(b => b.SuggestedDurationDays)) : 3;

        var argsJson = JsonSerializer.Serialize(new
        {
            planId,
            sourceType = command.SourceType,
            sourceId = command.SourceId,
            branches
        });

        var confirmationToken = confirmationTokenService.CreateConfirmationToken(
            tenantId,
            userId,
            BranchingToolName,
            argsJson,
            TimeSpan.FromMinutes(30));

        var interaction = await store.RecordInteractionAsync(
            tenantId,
            userId,
            "ai.branching",
            modelClient?.IsConfigured == true ? "ai-agent-branching" : "heuristic-branching",
            combinedText.Length > 200 ? combinedText[..200] : combinedText,
            $"Branches: {branches.Count}",
            AIInteractionStatus.Succeeded,
            null,
            null,
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId,
            interaction.Id,
            RecommendationObjectType.Request,
            command.SourceId,
            "BRANCHING_PLAN",
            argsJson,
            confidence,
            cancellationToken);

        return new BranchingPlanResult(
            PlanId: planId,
            SourceType: command.SourceType,
            SourceId: command.SourceId,
            OverallStrategy: overallStrategy,
            TotalBranches: branches.Count,
            EstimatedTotalDays: Math.Max(1, totalDays),
            Branches: branches,
            Confidence: confidence,
            ConfirmationToken: confirmationToken);
    }

    public async Task<ExecuteBranchingPlanResult> ExecuteBranchingPlanAsync(
        ExecuteBranchingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        if (command.Branches == null || command.Branches.Count == 0)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Branching plan must contain at least one branch.");
        }

        var argsJson = JsonSerializer.Serialize(new
        {
            planId = command.PlanId,
            sourceType = command.SourceType,
            sourceId = command.SourceId,
            branches = command.Branches
        });

        // Validate HMAC security token
        confirmationTokenService.ValidateAndConsumeToken(
            command.ConfirmationToken,
            tenantId,
            userId,
            BranchingToolName,
            argsJson);

        var hasRequest = command.Branches.Any(b => string.Equals(b.BranchType, "Request", StringComparison.OrdinalIgnoreCase));
        var hasTask = command.Branches.Any(b => string.Equals(b.BranchType, "Task", StringComparison.OrdinalIgnoreCase));

        if (hasRequest)
        {
            await authorizer.AuthorizeAsync("requests.create", new(tenantId), cancellationToken: cancellationToken);
        }
        if (hasTask)
        {
            await authorizer.AuthorizeAsync("tasks.create", new(tenantId), cancellationToken: cancellationToken);
        }

        var now = clock.GetUtcNow();
        var createdRecords = await branchingStore.PersistBranchingPlanAsync(
            tenantId,
            userId,
            command.PlanId,
            command.SourceType,
            command.SourceId,
            command.Branches,
            now,
            cancellationToken);

        return new ExecuteBranchingPlanResult(
            PlanId: command.PlanId,
            SourceId: command.SourceId,
            TotalCreated: createdRecords.Count,
            CreatedRecords: createdRecords,
            ExecutedAt: now);
    }

    private static List<BranchPlanNode> BuildDeterministicBranches(string text, AiTenantContextData context)
    {
        var branches = new List<BranchPlanNode>();
        var lines = text.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .ToList();

        if (lines.Count <= 1)
        {
            lines = Regex.Split(text, @"(?:\d+\.|\-|\*|\•)\s+")
                .Select(l => l.Trim())
                .Where(l => l.Length > 3)
                .ToList();
        }

        if (lines.Count == 0)
        {
            lines.Add(text);
        }

        var idx = 0;
        foreach (var line in lines.Take(5))
        {
            var isSupport = Regex.IsMatch(line, @"\b(hỗ trợ|cấp|yêu cầu|mua|thiết bị|it|hr|nhân sự|kế toán|tài chính|phê duyệt)\b", RegexOptions.IgnoreCase);
            var branchType = isSupport ? "Request" : "Task";

            var matchedDept = context.Departments.FirstOrDefault(d =>
                line.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                (d.Code != null && line.Contains(d.Code, StringComparison.OrdinalIgnoreCase)));

            var matchedService = context.Services.FirstOrDefault(s =>
                line.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ||
                s.Categories.Any(c => line.Contains(c.Name, StringComparison.OrdinalIgnoreCase)));

            var matchedCat = matchedService?.Categories.FirstOrDefault(c => line.Contains(c.Name, StringComparison.OrdinalIgnoreCase))
                ?? matchedService?.Categories.FirstOrDefault();

            var priority = Regex.IsMatch(line, @"\b(khẩn|gấp|urgent|asap|ngay)\b", RegexOptions.IgnoreCase) ? "High" : "Medium";
            var phase = idx == 0 ? 1 : (idx < 3 ? 1 : 2);
            var title = line.Length > 80 ? line[..80].Trim() + "…" : line;

            var deps = phase > 1 ? new List<int> { 0 } : new List<int>();

            branches.Add(new BranchPlanNode(
                BranchIndex: idx,
                BranchType: branchType,
                Title: title,
                Description: line,
                DepartmentId: matchedDept?.DepartmentId ?? context.Departments.FirstOrDefault()?.DepartmentId,
                DepartmentName: matchedDept?.Name ?? context.Departments.FirstOrDefault()?.Name,
                ServiceId: matchedService?.ServiceId ?? context.Services.FirstOrDefault()?.ServiceId,
                ServiceName: matchedService?.Name ?? context.Services.FirstOrDefault()?.Name,
                CategoryId: matchedCat?.CategoryId,
                CategoryName: matchedCat?.Name,
                Priority: priority,
                SuggestedDurationDays: 3 + idx,
                ExecutionPhase: phase,
                DependsOnBranchIndexes: deps,
                Checklist: new[]
                {
                    $"Giai đoạn {phase}: Tiếp nhận nội dung {title}",
                    $"Thực hiện xử lý theo phân nhánh {branchType}",
                    "Kiểm tra chất lượng và hoàn tất bàn giao"
                },
                Reasoning: isSupport ? "Cần điều phối hỗ trợ từ bộ phận chuyên trách" : "Công việc triển khai trực tiếp của nhóm"));

            idx++;
        }

        return branches;
    }
}
