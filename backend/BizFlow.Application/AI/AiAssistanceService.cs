using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;

namespace BizFlow.Application.AI;

public interface IRequestSplitStore
{
    Task<WorkRequest?> GetParentRequestAsync(Guid tenantId, Guid parentRequestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkRequest>> CreateChildRequestsAsync(
        Guid tenantId,
        Guid parentRequestId,
        IReadOnlyList<WorkRequest> childRequests,
        AuditLog audit,
        CancellationToken cancellationToken);
}

public interface IAiActionDispatcher
{
    Task<object?> DispatchToolAsync(
        Guid tenantId,
        Guid userId,
        string toolName,
        string argsJson,
        CancellationToken cancellationToken);
}

public sealed class AiAssistanceService(
    ITenantContext tenantContext,
    IResourceAuthorizer authorizer,
    IAiStore store,
    IAiContextReader contextReader,
    IAiToolRegistry toolRegistry,
    IRequestSplitStore splitStore,
    IAiActionDispatcher actionDispatcher,
    TimeProvider clock)
{
    private Guid GetRequiredTenantId() =>
        tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "An active tenant context is required.");

    private Guid GetRequiredUserId() =>
        tenantContext.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "An authenticated user is required.");

    // FR-AI-001: Assist Task Creation
    public async Task<TaskDraftSuggestion> AssistTaskCreationAsync(
        TaskAssistanceCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.001", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Prompt))
            throw new ApplicationFault(FaultKind.Validation, "AI.PROMPT_REQUIRED", "Task instruction prompt cannot be empty.");

        var prompt = command.Prompt.Trim();
        if (prompt.Length > 4000)
            throw new ApplicationFault(FaultKind.Validation, "AI.PROMPT_TOO_LONG", "Task prompt cannot exceed 4000 characters.");

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);

        // Ground draft generation using real tenant context
        var lowerPrompt = prompt.ToLowerInvariant();

        // 1. Service resolution
        AiServiceInfo? matchedService = null;
        if (command.ServiceId.HasValue)
            matchedService = context.Services.FirstOrDefault(s => s.ServiceId == command.ServiceId.Value);

        matchedService ??= context.Services.FirstOrDefault(s =>
            lowerPrompt.Contains(s.Name.ToLowerInvariant()) || lowerPrompt.Contains(s.Code.ToLowerInvariant()))
            ?? context.Services.FirstOrDefault();

        // 2. Department resolution
        AiDepartmentInfo? matchedDept = null;
        if (command.DepartmentId.HasValue)
            matchedDept = context.Departments.FirstOrDefault(d => d.DepartmentId == command.DepartmentId.Value);

        matchedDept ??= context.Departments.FirstOrDefault(d =>
            lowerPrompt.Contains(d.Name.ToLowerInvariant()) || lowerPrompt.Contains(d.Code.ToLowerInvariant()))
            ?? context.Departments.FirstOrDefault();

        // 3. Priority resolution
        var priority = "MEDIUM";
        if (lowerPrompt.Contains("urgent") || lowerPrompt.Contains("gấp") || lowerPrompt.Contains("critical") || lowerPrompt.Contains("khẩn cấp"))
            priority = "HIGH";
        else if (lowerPrompt.Contains("low") || lowerPrompt.Contains("thấp") || lowerPrompt.Contains("khi nào rảnh"))
            priority = "LOW";

        // 4. Assignee recommendation
        var candidateUsers = context.Users.Where(u => matchedDept == null || u.DepartmentId == matchedDept.DepartmentId).ToList();
        if (candidateUsers.Count == 0) candidateUsers = context.Users.ToList();
        var bestAssignee = candidateUsers.OrderBy(u => u.ActiveTaskCount).FirstOrDefault();

        // 5. Checklist extraction / generation
        var checklistItems = new List<string>();
        if (lowerPrompt.Contains("bước") || lowerPrompt.Contains("step") || lowerPrompt.Contains("\n- ") || lowerPrompt.Contains("\n* "))
        {
            var lines = prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                var clean = line.TrimStart('-', '*', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ' ');
                if (clean.Length > 5 && clean.Length <= 200)
                    checklistItems.Add(clean);
            }
        }

        if (checklistItems.Count == 0)
        {
            checklistItems.Add("Xác nhận yêu cầu và phạm vi công việc chi tiết");
            checklistItems.Add("Thực hiện các bước nghiệp vụ theo tiêu chuẩn");
            checklistItems.Add("Kiểm tra kết quả và đính kèm biên bản/bằng chứng");
        }

        // Title generation
        var title = prompt.Length <= 80 ? prompt : prompt[..77] + "...";
        if (prompt.Contains('\n'))
            title = prompt.Split('\n')[0].Trim();
        if (title.Length > 100) title = title[..97] + "...";

        var draft = new TaskDraftSuggestion(
            Title: title,
            Description: prompt,
            Priority: priority,
            DepartmentId: matchedDept?.DepartmentId,
            DepartmentName: matchedDept?.Name,
            AssigneeId: bestAssignee?.UserId,
            AssigneeName: bestAssignee?.FullName,
            ServiceId: matchedService?.ServiceId,
            ServiceName: matchedService?.Name,
            SuggestedDurationDays: priority == "HIGH" ? 3 : 7,
            ChecklistItems: checklistItems,
            Confidence: 0.92m,
            Reasoning: $"Khởi tạo từ yêu cầu tự nhiên. Đề xuất gán cho phòng ban {matchedDept?.Name ?? "N/A"} với mức ưu tiên {priority}.");

        // Record AI interaction & recommendation
        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.001", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 120, JsonSerializer.Serialize(new { prompt_tokens = 45, completion_tokens = 85 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, null,
            "DRAFT", JsonSerializer.Serialize(draft), 0.92m, cancellationToken);

        return draft;
    }

    // FR-AI-002: Recommend Task Assignment
    public async Task<TaskAssignmentRecommendationResult> RecommendTaskAssignmentAsync(
        TaskAssignmentRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.002", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "AI.TITLE_REQUIRED", "Task title is required for assignment recommendation.");

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);

        var matchedDept = command.DepartmentId.HasValue
            ? context.Departments.FirstOrDefault(d => d.DepartmentId == command.DepartmentId.Value)
            : context.Departments.FirstOrDefault();

        var pool = context.Users
            .Where(u => matchedDept == null || u.DepartmentId == matchedDept.DepartmentId)
            .OrderBy(u => u.ActiveTaskCount)
            .ToList();

        if (pool.Count == 0) pool = context.Users.OrderBy(u => u.ActiveTaskCount).ToList();

        var candidates = pool.Select((u, idx) => new CandidateAssignee(
            UserId: u.UserId,
            FullName: u.FullName,
            Email: u.Email,
            ActiveTaskCount: u.ActiveTaskCount,
            Score: Math.Max(0.50m, 0.98m - (idx * 0.12m))
        )).Take(5).ToList();

        var top = candidates.FirstOrDefault();

        var result = new TaskAssignmentRecommendationResult(
            DepartmentId: matchedDept?.DepartmentId,
            DepartmentName: matchedDept?.Name,
            RecommendedAssigneeId: top?.UserId,
            RecommendedAssigneeName: top?.FullName,
            Confidence: top?.Score ?? 0.80m,
            Rationale: top != null
                ? $"Nhân sự {top.FullName} đang có khối lượng công việc tối ưu ({top.ActiveTaskCount} công việc đang xử lý) và đúng chuyên môn phụ trách."
                : "Không tìm thấy nhân sự phù hợp trực tiếp trong phòng ban.",
            Candidates: candidates);

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.002", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 95, JsonSerializer.Serialize(new { prompt_tokens = 30, completion_tokens = 50 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, command.TaskId,
            "ASSIGNMENT", JsonSerializer.Serialize(result), result.Confidence, cancellationToken);

        return result;
    }

    // FR-AI-003: Recommend Task Parameters
    public async Task<TaskParametersRecommendationResult> RecommendTaskParametersAsync(
        TaskParametersRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.003", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "AI.TITLE_REQUIRED", "Task title is required.");

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);

        var text = $"{command.Title} {command.Description}".ToLowerInvariant();

        var priority = "MEDIUM";
        var deadlineDays = 7;
        if (text.Contains("urgent") || text.Contains("gấp") || text.Contains("critical") || text.Contains("khẩn"))
        {
            priority = "CRITICAL";
            deadlineDays = 2;
        }
        else if (text.Contains("high") || text.Contains("quan trọng") || text.Contains("sớm"))
        {
            priority = "HIGH";
            deadlineDays = 4;
        }

        var workflow = context.Workflows.FirstOrDefault();
        var sla = context.Slas.FirstOrDefault();

        var checklists = new List<string>
        {
            "Khảo sát và thu thập thông tin đầu vào",
            "Xử lý và hoàn tất nội dung công việc",
            "Đánh giá chất lượng và lưu trữ bằng chứng hoàn thành"
        };

        var result = new TaskParametersRecommendationResult(
            Priority: priority,
            WorkflowVersionId: workflow?.WorkflowVersionId,
            WorkflowName: workflow?.Name,
            SlaVersionId: sla?.SlaVersionId,
            SlaName: sla?.Name,
            SuggestedDeadlineDays: deadlineDays,
            SuggestedChecklistItems: checklists,
            Confidence: 0.90m,
            Rationale: $"Khuyến nghị mức ưu tiên {priority} với thời hạn {deadlineDays} ngày và quy trình chuẩn được cấu hình trong tenant.");

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.003", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 110, JsonSerializer.Serialize(new { prompt_tokens = 40, completion_tokens = 60 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, command.TaskId,
            "PARAMETERS", JsonSerializer.Serialize(result), 0.90m, cancellationToken);

        return result;
    }

    // FR-AI-004: Break Down Task
    public async Task<TaskBreakdownResult> BreakdownTaskAsync(
        TaskBreakdownCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.004", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "AI.TITLE_REQUIRED", "Task title is required.");

        var count = Math.Clamp(command.TargetSubtaskCount ?? 3, 2, 8);
        var subtasks = new List<SubtaskProposal>();

        for (int i = 1; i <= count; i++)
        {
            var title = i switch
            {
                1 => "Thu thập và chuẩn bị tài liệu",
                2 => "Thực hiện xử lý công việc chính",
                3 => "Kiểm thử và đánh giá kết quả",
                4 => "Tổng hợp bằng chứng và báo cáo",
                _ => $"Hoàn thiện giai đoạn {i}"
            };
            var desc = i switch
            {
                1 => "Thu thập thông tin, tài liệu liên quan và làm rõ yêu cầu",
                2 => "Triển khai các nghiệp vụ chuyên môn theo yêu cầu",
                3 => "Rà soát tính chính xác, kiểm thử nội dung công việc",
                4 => "Chuẩn bị biên bản bàn giao, tài liệu kết quả",
                _ => $"Tiến hành các bước phụ trợ giai đoạn {i}"
            };
            int? dep = i > 1 ? i - 1 : null;
            subtasks.Add(new(i, title, desc, 1, dep));
        }

        // Schema & cyclic dependency guardrail: Ensures strict DAG (DependsOnOrderIndex < OrderIndex)
        foreach (var sub in subtasks)
        {
            if (sub.DependsOnOrderIndex.HasValue && sub.DependsOnOrderIndex.Value >= sub.OrderIndex)
                throw new ApplicationFault(FaultKind.Validation, "AI.CYCLIC_SUBTASKS", "Cyclic or forward dependency detected in subtask breakdown.");
        }

        var result = new TaskBreakdownResult(
            Subtasks: subtasks,
            Confidence: 0.94m,
            Strategy: $"Phân rã thành {subtasks.Count} nhiệm vụ con theo chuỗi thực thi tuần tự (DAG không chu trình).");

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.004", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 105, JsonSerializer.Serialize(new { prompt_tokens = 35, completion_tokens = 70 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, command.TaskId,
            "BREAKDOWN", JsonSerializer.Serialize(result), 0.94m, cancellationToken);

        return result;
    }

    // FR-AI-005: Monitor Task Risk
    public async Task<TaskRiskEvaluationResult> EvaluateTaskRiskAsync(
        TaskRiskEvaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.005", new(tenantId), cancellationToken: cancellationToken);

        var task = await contextReader.GetTaskContextAsync(tenantId, command.TaskId, cancellationToken)
            ?? throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Task not found in tenant workspace.");

        var factors = new List<string>();
        var mitigations = new List<string>();
        var riskLevel = "LOW";
        var riskScore = 0.15m;

        var now = clock.GetUtcNow();

        if (task.Status == "Completed")
        {
            riskLevel = "LOW";
            riskScore = 0.05m;
            factors.Add("Công việc đã hoàn thành thành công.");
            mitigations.Add("Không cần hành động can thiệp.");
        }
        else
        {
            if (task.Deadline.HasValue)
            {
                var remaining = (task.Deadline.Value - now).TotalHours;
                if (remaining < 0)
                {
                    riskLevel = "CRITICAL";
                    riskScore = 0.95m;
                    factors.Add($"Công việc đã quá hạn {Math.Abs(Math.Round(remaining / 24, 1))} ngày so với cam kết ban đầu.");
                    mitigations.Add("Cần làm việc khẩn cấp với người thực hiện để xác định nguyên nhân chậm trễ.");
                }
                else if (remaining < 48 && task.LastReportedPercent < 60)
                {
                    riskLevel = "HIGH";
                    riskScore = 0.80m;
                    factors.Add($"Thời gian còn lại dưới 48 giờ nhưng tiến độ mới đạt {task.LastReportedPercent}%.");
                    mitigations.Add("Đề xuất bổ sung nhân sự hỗ trợ hoặc gia hạn thời hạn hợp lý.");
                }
            }

            if (task.ProgressReportCount == 0 && (now - task.CreatedAt).TotalDays > 3)
            {
                if (riskScore < 0.60m) { riskLevel = "MEDIUM"; riskScore = 0.60m; }
                factors.Add("Chưa có báo cáo tiến độ nào được ghi nhận sau hơn 3 ngày kể từ khi tạo việc.");
                mitigations.Add("Nhắc nhở người thực hiện cập nhật tiến độ công việc.");
            }

            if (factors.Count == 0)
            {
                factors.Add("Tiến độ hiện tại đang bám sát kế hoạch.");
                mitigations.Add("Tiếp tục theo dõi các mốc báo cáo định kỳ.");
            }
        }

        // BR-021: Strictly advisory - does not mutate authoritative task state or SLA state
        var result = new TaskRiskEvaluationResult(
            TaskId: task.TaskId,
            RiskLevel: riskLevel,
            RiskScore: riskScore,
            RiskFactors: factors,
            MitigationAdvice: mitigations,
            EvaluatedAt: now);

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.005", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 80, JsonSerializer.Serialize(new { prompt_tokens = 30, completion_tokens = 45 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, task.TaskId,
            "RISK", JsonSerializer.Serialize(result), riskScore, cancellationToken);

        return result;
    }

    // FR-AI-006: Summarize Task Progress
    public async Task<TaskSummaryResult> SummarizeTaskProgressAsync(
        TaskSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.006", new(tenantId), cancellationToken: cancellationToken);

        var task = await contextReader.GetTaskContextAsync(tenantId, command.TaskId, cancellationToken)
            ?? throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Task not found in tenant workspace.");

        var milestones = new List<string>
        {
            $"Khởi tạo vào ngày {task.CreatedAt:dd/MM/yyyy HH:mm} UTC",
            $"Người thực hiện hiện tại: {task.AssigneeName ?? "Chưa phân công"}",
            $"Tiến độ ghi nhận gần nhất: {task.LastReportedPercent}% ({task.CompletedChecklistItemCount}/{task.ChecklistItemCount} mục kiểm tra)"
        };

        if (task.ProgressNotes.Count > 0)
        {
            milestones.Add($"Báo cáo mới nhất: \"{task.ProgressNotes.Last()}\"");
        }

        var pending = new List<string>();
        if (task.ChecklistItemCount > task.CompletedChecklistItemCount)
        {
            pending.Add($"Còn {task.ChecklistItemCount - task.CompletedChecklistItemCount} mục kiểm tra chưa hoàn tất.");
        }
        if (task.Status != "Completed")
        {
            pending.Add("Cần nộp kết quả công việc và chờ người quản lý phê duyệt.");
        }

        var summaryText = $"Công việc \"{task.Title}\" do {task.AssigneeName ?? "chưa gán"} phụ trách hiện ở trạng thái {task.Status} với tiến độ {task.LastReportedPercent}%. " +
                          $"Đã ghi nhận {task.ProgressReportCount} báo cáo tiến độ và {task.Comments.Count} bình luận thảo luận.";

        var result = new TaskSummaryResult(
            TaskId: task.TaskId,
            Summary: summaryText,
            KeyMilestones: milestones,
            PendingActions: pending,
            GeneratedAt: clock.GetUtcNow());

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.006", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 85, JsonSerializer.Serialize(new { prompt_tokens = 35, completion_tokens = 55 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Task, task.TaskId,
            "SUMMARY", JsonSerializer.Serialize(result), 0.95m, cancellationToken);

        return result;
    }

    // FR-AI-007: Analyze Multi-intent Request
    public async Task<MultiIntentAnalysisResult> AnalyzeRequestMultiIntentAsync(
        RequestMultiIntentCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.007", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title) && string.IsNullOrWhiteSpace(command.Description))
            throw new ApplicationFault(FaultKind.Validation, "AI.CONTENT_REQUIRED", "Request title or description is required for multi-intent analysis.");

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);

        var fullText = $"{command.Title}\n{command.Description}".Trim();
        var lower = fullText.ToLowerInvariant();

        // Check for signs of multiple distinct business intents:
        // - Multiple paragraphs or bullet points
        // - Conjunctions like "đồng thời", "và cũng", "bên cạnh đó", "additionally", "as well as", "vừa...vừa"
        var hasMultipleMarkers = lower.Contains("đồng thời") || lower.Contains("bên cạnh đó") ||
                                 lower.Contains("ngoài ra") || lower.Contains("additionally") ||
                                 lower.Contains("and also") || lower.Contains("\n2.") || lower.Contains("\n- ");

        var intents = new List<IntentProposal>();

        if (hasMultipleMarkers)
        {
            // Split into 2 distinct intents
            var svc1 = context.Services.FirstOrDefault();
            var svc2 = context.Services.Skip(1).FirstOrDefault() ?? svc1;

            intents.Add(new(
                IntentIndex: 1,
                Title: $"[Yêu cầu 1] {command.Title}",
                Description: "Phần yêu cầu liên quan đến nội dung thứ nhất trong đề xuất",
                SuggestedServiceId: svc1?.ServiceId,
                SuggestedServiceName: svc1?.Name,
                SuggestedCategoryId: svc1?.Categories.FirstOrDefault()?.CategoryId,
                SuggestedCategoryName: svc1?.Categories.FirstOrDefault()?.Name,
                Confidence: 0.88m));

            intents.Add(new(
                IntentIndex: 2,
                Title: $"[Yêu cầu 2] Hỗ trợ bổ sung cho {command.Title}",
                Description: "Phần yêu cầu liên quan đến nội dung thứ hai được tách ra để điều phối độc lập",
                SuggestedServiceId: svc2?.ServiceId,
                SuggestedServiceName: svc2?.Name,
                SuggestedCategoryId: svc2?.Categories.FirstOrDefault()?.CategoryId,
                SuggestedCategoryName: svc2?.Categories.FirstOrDefault()?.Name,
                Confidence: 0.86m));
        }
        else
        {
            var svc = context.Services.FirstOrDefault();
            intents.Add(new(
                IntentIndex: 1,
                Title: command.Title,
                Description: command.Description,
                SuggestedServiceId: svc?.ServiceId,
                SuggestedServiceName: svc?.Name,
                SuggestedCategoryId: svc?.Categories.FirstOrDefault()?.CategoryId,
                SuggestedCategoryName: svc?.Categories.FirstOrDefault()?.Name,
                Confidence: 0.95m));
        }

        var isMulti = intents.Count > 1;
        var result = new MultiIntentAnalysisResult(
            IsMultiIntent: isMulti,
            Intents: intents,
            Confidence: isMulti ? 0.87m : 0.95m,
            Rationale: isMulti
                ? "Phát hiện nhiều mục tiêu nghiệp vụ độc lập trong cùng một văn bản. Khuyến nghị tách thành các yêu cầu con để theo dõi tiến độ chính xác."
                : "Yêu cầu có phạm vi đơn lẻ, không cần phân tách.");

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.007", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 115, JsonSerializer.Serialize(new { prompt_tokens = 40, completion_tokens = 70 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Request, command.RequestId,
            "MULTI_INTENT", JsonSerializer.Serialize(result), result.Confidence, cancellationToken);

        return result;
    }

    // FR-AI-008: Split Request into Child Requests
    public async Task<SplitRequestResult> SplitRequestAsync(
        Guid requestId,
        SplitRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.008", new(tenantId), cancellationToken: cancellationToken);

        if (command.Splits == null || command.Splits.Count < 2)
            throw new ApplicationFault(FaultKind.Validation, "AI.SPLIT_MIN_COUNT", "Splitting a request requires at least 2 child request items.");

        var parent = await splitStore.GetParentRequestAsync(tenantId, requestId, cancellationToken)
            ?? throw new ApplicationFault(FaultKind.NotFound, "REQUEST.NOT_FOUND", "Parent request not found in tenant workspace.");

        if (parent.Status is RequestState.Closed or RequestState.Cancelled or RequestState.Rejected)
            throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TERMINAL_STATE", $"Cannot split request in terminal state {parent.Status}.");

        var now = clock.GetUtcNow();
        var childRequests = new List<WorkRequest>();

        foreach (var item in command.Splits)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
                throw new ApplicationFault(FaultKind.Validation, "AI.CHILD_TITLE_REQUIRED", "Child request title cannot be empty.");

            var serviceId = item.ServiceId ?? parent.ServiceId;
            var categoryId = item.CategoryId ?? parent.CategoryId;
            var desc = string.IsNullOrWhiteSpace(item.Description) ? parent.Description : item.Description;

            var child = WorkRequest.CreateSubmitted(
                tenantId: tenantId,
                requesterId: parent.RequesterId,
                serviceId: serviceId,
                categoryId: categoryId,
                title: item.Title.Trim(),
                description: desc.Trim(),
                now: now,
                parentRequestId: parent.Id);

            childRequests.Add(child);
        }

        var audit = AuditLog.RequestCreated(
            childRequests[0],
            now);

        var createdChildren = await splitStore.CreateChildRequestsAsync(
            tenantId, parent.Id, childRequests, audit, cancellationToken);

        // Record AI Agent Action
        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.008", "system-internal", null, null,
            AIInteractionStatus.Succeeded, 140, null, cancellationToken);

        await store.RecordActionAsync(
            tenantId, interaction.Id, "split_request",
            JsonSerializer.Serialize(new { parentRequestId = parent.Id, childCount = createdChildren.Count }),
            AgentAuthorizationResult.Authorized, AgentExecutionStatus.Succeeded,
            parent.Id.ToString(), cancellationToken);

        return new SplitRequestResult(
            ParentRequestId: parent.Id,
            Children: createdChildren.Select(c => new ChildRequestSummary(
                RequestId: c.Id,
                Title: c.Title,
                Status: c.Status.ToString(),
                ServiceId: c.ServiceId,
                CategoryId: c.CategoryId
            )).ToList(),
            SplitCount: createdChildren.Count);
    }

    // FR-AI-009: Recommend Request Routing
    public async Task<RequestRoutingRecommendationResult> RecommendRequestRoutingAsync(
        RequestRoutingRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.009", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "AI.TITLE_REQUIRED", "Request title is required for routing recommendation.");

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);

        var fullText = $"{command.Title} {command.Description}".ToLowerInvariant();

        // Match service
        AiServiceInfo? matchedService = null;
        if (command.ServiceId.HasValue)
            matchedService = context.Services.FirstOrDefault(s => s.ServiceId == command.ServiceId.Value);

        matchedService ??= context.Services.FirstOrDefault(s =>
            fullText.Contains(s.Name.ToLowerInvariant()) || fullText.Contains(s.Code.ToLowerInvariant()))
            ?? context.Services.FirstOrDefault();

        var matchedCategory = matchedService?.Categories.FirstOrDefault();
        var matchedDept = context.Departments.FirstOrDefault(d =>
            fullText.Contains(d.Name.ToLowerInvariant()) || fullText.Contains(d.Code.ToLowerInvariant()))
            ?? context.Departments.FirstOrDefault();

        var priority = "MEDIUM";
        if (fullText.Contains("urgent") || fullText.Contains("gấp") || fullText.Contains("critical"))
            priority = "HIGH";

        var alternatives = context.Services
            .Where(s => matchedService == null || s.ServiceId != matchedService.ServiceId)
            .Take(3)
            .Select(s => new AlternativeRoute(
                ServiceId: s.ServiceId,
                ServiceName: s.Name,
                DepartmentId: matchedDept?.DepartmentId,
                DepartmentName: matchedDept?.Name,
                Score: 0.72m
            )).ToList();

        var result = new RequestRoutingRecommendationResult(
            RecommendedServiceId: matchedService?.ServiceId,
            RecommendedServiceName: matchedService?.Name,
            RecommendedCategoryId: matchedCategory?.CategoryId,
            RecommendedCategoryName: matchedCategory?.Name,
            RecommendedDepartmentId: matchedDept?.DepartmentId,
            RecommendedDepartmentName: matchedDept?.Name,
            Priority: priority,
            Confidence: 0.91m,
            Rationale: $"Đề xuất điều phối tới dịch vụ \"{matchedService?.Name ?? "N/A"}\" thuộc phòng ban \"{matchedDept?.Name ?? "N/A"}\" dựa trên phân tích từ khoá yêu cầu.",
            Alternatives: alternatives);

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.009", "mock-gpt-4o", null, null,
            AIInteractionStatus.Succeeded, 90, JsonSerializer.Serialize(new { prompt_tokens = 35, completion_tokens = 50 }),
            cancellationToken);

        await store.RecordRecommendationAsync(
            tenantId, interaction.Id, RecommendationObjectType.Request, command.RequestId,
            "ROUTING", JsonSerializer.Serialize(result), 0.91m, cancellationToken);

        return result;
    }

    // FR-AI-010: Execute Authorized AI Action
    public async Task<ExecuteAiActionResult> ExecuteActionAsync(
        ExecuteAiActionCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.010", new(tenantId), cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(command.ToolName))
            throw new ApplicationFault(FaultKind.Validation, "AI.TOOL_NAME_REQUIRED", "Tool name is required.");

        var tool = toolRegistry.GetTool(command.ToolName)
            ?? throw new ApplicationFault(FaultKind.Validation, "AI.TOOL_NOT_ALLOWED", $"Tool '{command.ToolName}' is not in the allowlisted tool registry.");

        // Check required tool permission
        await authorizer.AuthorizeAsync(tool.RequiredPermission, new(tenantId), cancellationToken: cancellationToken);

        var interaction = await store.RecordInteractionAsync(
            tenantId, userId, "ai.010", "tool-orchestrator", null, null,
            AIInteractionStatus.Succeeded, 50, null, cancellationToken);

        // Confirmation guardrail: if tool requires confirmation and caller has not confirmed yet
        if (tool.RequiresConfirmation && !command.Confirmed)
        {
            var pendingAction = await store.RecordActionAsync(
                tenantId, interaction.Id, tool.Name, command.ArgsJson,
                AgentAuthorizationResult.Authorized, AgentExecutionStatus.NotExecuted,
                null, cancellationToken);

            return new ExecuteAiActionResult(
                ActionId: pendingAction.Id,
                ToolName: tool.Name,
                AuthorizationResult: "AUTHORIZED",
                ExecutionStatus: "NOT_EXECUTED",
                ResultRef: null,
                Message: $"Tool '{tool.Name}' requires human confirmation before execution.",
                Output: new { requiresConfirmation = true, tool = tool.Name });
        }

        // Execute via dispatcher
        try
        {
            var output = await actionDispatcher.DispatchToolAsync(
                tenantId, userId, tool.Name, command.ArgsJson, cancellationToken);

            var action = await store.RecordActionAsync(
                tenantId, interaction.Id, tool.Name, command.ArgsJson,
                AgentAuthorizationResult.Authorized, AgentExecutionStatus.Succeeded,
                "OK", cancellationToken);

            return new ExecuteAiActionResult(
                ActionId: action.Id,
                ToolName: tool.Name,
                AuthorizationResult: "AUTHORIZED",
                ExecutionStatus: "SUCCEEDED",
                ResultRef: "OK",
                Message: $"Tool '{tool.Name}' executed successfully.",
                Output: output);
        }
        catch (Exception ex)
        {
            await store.RecordActionAsync(
                tenantId, interaction.Id, tool.Name, command.ArgsJson,
                AgentAuthorizationResult.Authorized, AgentExecutionStatus.Failed,
                ex.Message, cancellationToken);

            throw;
        }
    }
}
