using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;

namespace BizFlow.Application.AI;

public sealed partial class RobotChatService(
    ITenantContext tenantContext,
    TaskListQuery taskListQuery,
    NotificationInbox notificationInbox,
    IAiTaskDecompositionService decompositionService,
    TimeProvider clock,
    IAiModelClient? modelClient = null,
    IAiToolRegistry? toolRegistry = null,
    AiAssistanceService? assistanceService = null,
    IAiVectorMemoryStore? vectorMemoryStore = null,
    IProactiveAgentScanner? proactiveScanner = null,
    IAiContextReader? contextReader = null,
    IAiLearnedChoiceStore? learnedChoiceStore = null,
    IConversationStateManager? conversationStateManager = null,
    IAgentNotificationService? agentNotificationService = null,
    BizFlow.Application.Organization.UserDirectoryQuery? userDirectoryQuery = null,
    BizFlow.Application.Requests.RequestListQuery? requestListQuery = null,
    TaskDetailQuery? taskDetailQuery = null,
    BizFlow.Application.Requests.RequestDetailQuery? requestDetailQuery = null,
    BizFlow.Application.Workflows.IWorkflowRuntimeStore? workflowRuntime = null) : IRobotChatService
{
    public async Task<RobotChatResponse> ProcessMessageAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken = default)
    {
        var rawResponse = await ExecuteInternalProcessAsync(command, cancellationToken);
        return SanitizeResponse(rawResponse);
    }

    private static string CleanOutputText(string? text, bool preserveFormatting = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // The chat renderer supports headings, lists and emphasis as escaped text.
        // Keep formatting in the response; labels and questionnaire fields stay plain.
        var cleaned = preserveFormatting ? text : Regex.Replace(text, @"\*{1,3}", "");
        // Remove markdown blockquote angle bracket at line start
        cleaned = Regex.Replace(cleaned, @"(?m)^>\s*", "");
        // Remove all unicode emojis (emoticons, pictographs, transport, misc symbols, dingbats)
        cleaned = Regex.Replace(cleaned, @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u26FF\u2700-\u27BF\uFE00-\uFE0F]", "");
        // Clean excessive spaces while preserving line breaks
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        return cleaned.Trim();
    }

    private static RobotChatResponse SanitizeResponse(RobotChatResponse resp)
    {
        var cleanedResponse = CleanOutputText(resp.Response, preserveFormatting: true);
        var cleanedThought = resp.AgentThought != null ? CleanOutputText(resp.AgentThought) : null;
        var cleanedCards = resp.ActionCards?
            .Select(c => c with { Title = CleanOutputText(c.Title) })
            .ToList();

        AgentClarificationFormDto? cleanedForm = null;
        if (resp.Questionnaire != null)
        {
            var qForm = resp.Questionnaire;
            cleanedForm = new AgentClarificationFormDto(
                qForm.FormId,
                CleanOutputText(qForm.Topic),
                CleanOutputText(qForm.ContextExplanation),
                qForm.RiskAnalysis != null ? CleanOutputText(qForm.RiskAnalysis) : null,
                qForm.Questions.Select(q => new AgentQuestionDto(
                    q.Id,
                    CleanOutputText(q.Title),
                    q.Type,
                    q.Options?.Select(o => new AgentQuestionOptionDto(o.Id, CleanOutputText(o.Label), o.IsDefault)).ToList(),
                    q.Required,
                    q.Placeholder != null ? CleanOutputText(q.Placeholder) : null,
                    q.HelpText != null ? CleanOutputText(q.HelpText) : null,
                    q.AllowCustomInput)).ToList(),
                CleanOutputText(qForm.SubmitButtonText),
                qForm.SkipButtonText != null ? CleanOutputText(qForm.SkipButtonText) : null);
        }

        return new RobotChatResponse(
            cleanedResponse,
            resp.RecognizedCommand,
            cleanedCards,
            cleanedForm,
            cleanedThought);
    }

    private async Task<RobotChatResponse> ExecuteInternalProcessAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
            throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Vui lòng đăng nhập để sử dụng Trợ lý Robot.");

        var message = command.Message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(message))
            throw new ApplicationFault(FaultKind.Validation, "AI.MESSAGE_EMPTY", "Tin nhắn không được để trống.");

        // 1. Command /task
        if (message.StartsWith("/task", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleTaskCommandAsync(message, cancellationToken);
        }

        // 2. Command /notifications
        if (message.StartsWith("/notifications", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleNotificationsCommandAsync(message, cancellationToken);
        }

        // 3. Command /create-task
        if (message.StartsWith("/create-task", StringComparison.OrdinalIgnoreCase) && assistanceService != null)
        {
            return await HandleCreateTaskCommandAsync(message, cancellationToken);
        }

        // 4. Command /memory (Semantic Vector Memory RAG Query)
        if (message.StartsWith("/memory", StringComparison.OrdinalIgnoreCase) && vectorMemoryStore != null)
        {
            return await HandleMemoryCommandAsync(message, cancellationToken);
        }

        // 5. Command /proactive (Proactive Autonomous Risk & Workload Insights)
        if (message.StartsWith("/proactive", StringComparison.OrdinalIgnoreCase) && proactiveScanner != null)
        {
            return await HandleProactiveCommandAsync(cancellationToken);
        }

        // 6. Command /remember (Personal User Memory & Optimization)
        if ((vectorMemoryStore != null || learnedChoiceStore != null) && (
            message.StartsWith("/remember", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("nhớ rằng", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("ghi nhớ:", StringComparison.OrdinalIgnoreCase)))
        {
            return await HandleRememberCommandAsync(message, cancellationToken);
        }

        return await RunPersonalAgentAsync(command, cancellationToken);
    }

    private async Task<RobotChatResponse> HandleTaskCommandAsync(string message, CancellationToken cancellationToken)
    {
        var prompt = message["/task".Length..].Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new RobotChatResponse(
                "Bạn có thể sử dụng lệnh `/task <mô tả công việc>` để bóc tách kế hoạch thành các đầu việc cụ thể.\n" +
                "Ví dụ: `/task Tiếp nhận nhân sự mới: chuẩn bị laptop, cấp email công ty và ký HĐLĐ`.\n" +
                "Hoặc bạn có thể bấm nút bên dưới để mở giao diện Phân rã AI chuyên dụng.",
                "/task",
                [new RobotActionCard("OPEN_DECOMPOSITION", "Mở công cụ Phân rã AI", "/tasks/ai-decomposition")]);
        }

        var preview = await decompositionService.PreviewAsync(
            new TaskDecompositionPreviewCommand(prompt),
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Trợ lý Robot đã phân rã yêu cầu thành {preview.Proposals.Count} đề xuất công việc (Bản xem trước - Chưa lưu vào DB theo BR-028):");
        sb.AppendLine();

        for (var i = 0; i < preview.Proposals.Count; i++)
        {
            var p = preview.Proposals[i];
            var typeLabel = p.Type == "SupportRequest" ? "Yêu cầu hỗ trợ" : "Giao việc";
            sb.AppendLine($"{i + 1}. {p.Title} [{typeLabel}] - Mức ưu tiên: {p.Priority}");
            if (!string.IsNullOrWhiteSpace(p.DepartmentName))
            {
                sb.AppendLine($"   - Phòng ban: {p.DepartmentName}");
            }
            if (p.ChecklistItems is { Count: > 0 })
            {
                sb.AppendLine($"   - Các bước: {string.Join(", ", p.ChecklistItems)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Vui lòng mở giao diện Phân rã AI để kiểm tra, chỉnh sửa inline và xác nhận tạo việc.");

        return new RobotChatResponse(
            sb.ToString().TrimEnd(),
            "/task",
            [new RobotActionCard("OPEN_DECOMPOSITION", "Xem & Xác nhận tạo việc", "/tasks/ai-decomposition", new { prompt, proposals = preview.Proposals })]);
    }

    private async Task<RobotChatResponse> HandleNotificationsCommandAsync(string message, CancellationToken cancellationToken)
    {
        long unreadCount = 0;
        try
        {
            var notifPage = await notificationInbox.ListAsync(new NotificationFilter(1, 5, true), cancellationToken);
            unreadCount = notifPage.UnreadCount;
        }
        catch
        {
            // Ignore if user lacks permissions
        }

        var sb = new StringBuilder();
        sb.AppendLine("Quản lý Thông báo và Nhắc hẹn (Deadline Reminders)");
        sb.AppendLine();
        sb.AppendLine($"- Hiện tại bạn có {unreadCount} thông báo chưa đọc trong hòm thư.");
        sb.AppendLine("- Hệ thống hỗ trợ nhắc hạn đa kênh tự động tại 4 mốc: 24 giờ, 4 giờ, 1 giờ trước hạn và khi quá hạn.");
        sb.AppendLine("- Kênh gửi thông báo: In-App Toast (SignalR) khi online, và Web Push (VAPID) khi không mở website.");
        sb.AppendLine();
        sb.AppendLine("Bạn có thể xem chi tiết hoặc cài đặt nhắc nhở bằng cách mở trang Thông báo bên dưới.");

        return new RobotChatResponse(
            sb.ToString(),
            "/notifications",
            [new RobotActionCard("OPEN_NOTIFICATIONS", "Xem hòm thư thông báo", "/notifications")]);
    }

    private async Task<RobotChatResponse> HandleCreateTaskCommandAsync(string message, CancellationToken cancellationToken)
    {
        var title = message["/create-task".Length..].Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new RobotChatResponse(
                "Vui lòng nhập tiêu đề công việc sau lệnh `/create-task <tiêu đề>`. Ví dụ: `/create-task Soạn thảo báo cáo tuần`.",
                "/create-task");
        }

        try
        {
            var argsJson = JsonSerializer.Serialize(new { title });
            var result = await assistanceService!.ExecuteActionAsync(
                new ExecuteAiActionCommand("create_task_draft", argsJson, Confirmed: false),
                cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("Đề xuất tạo công việc mới:");
            sb.AppendLine($"- Tiêu đề: {title}");
            sb.AppendLine();
            sb.AppendLine("Hành động này cần sự xác nhận của bạn trước khi lưu vào hệ thống.");

            return new RobotChatResponse(
                sb.ToString().TrimEnd(),
                "/create-task",
                [
                    new RobotActionCard(
                        "CONFIRM_ACTION",
                        "Xác nhận tạo việc",
                        "/api/v1/ai/actions/execute",
                        new { toolName = "create_task_draft", confirmationToken = result.ConfirmationToken, argsJson })
                ]);
        }
        catch (ApplicationFault af)
        {
            return new RobotChatResponse($"Không thể đề xuất tạo việc: {af.Message}", "ACTION_REJECTED");
        }
    }

    private async Task<RobotChatResponse> HandleMemoryCommandAsync(string message, CancellationToken cancellationToken)
    {
        var query = message["/memory".Length..].Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            var recent = await vectorMemoryStore!.GetRecentMemoriesAsync(tenantContext.TenantId!.Value, null, 5, cancellationToken);
            if (recent.Count == 0)
            {
                return new RobotChatResponse(
                    "Bộ nhớ dài hạn & Tri thức doanh nghiệp (Vector Memory):\n" +
                    "Chưa có tài liệu chính sách hoặc tiền lệ công việc nào được lập chỉ mục.\n" +
                    "Bạn có thể tra cứu theo cú pháp: `/memory <nội dung cần tìm>`.",
                    "/memory");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Bộ nhớ dài hạn & Quy định nội bộ gần nhất:");
            foreach (var m in recent)
            {
                sb.AppendLine($"- [{m.MemoryScope}] {m.Title}: {Truncate(m.Content, 100)}");
            }
            sb.AppendLine("\nGõ `/memory <từ khóa>` để tìm kiếm tương đồng ngữ nghĩa (Cosine RAG).");
            return new RobotChatResponse(sb.ToString().TrimEnd(), "/memory");
        }

        var results = await vectorMemoryStore!.SearchMemoriesAsync(tenantContext.TenantId!.Value, query, null, 3, 0.30f, cancellationToken);
        if (results.Count == 0)
        {
            return new RobotChatResponse(
                $"Không tìm thấy chính sách hoặc tiền lệ nào khớp với nội dung '{query}' trong bộ nhớ doanh nghiệp.",
                "/memory");
        }

        var rsb = new StringBuilder();
        rsb.AppendLine("Kết quả tra cứu Bộ nhớ Vector (Khớp ngữ nghĩa cao nhất):");
        foreach (var r in results)
        {
            var matchPercent = Math.Round(r.SimilarityScore * 100, 1);
            rsb.AppendLine($"\n{r.Title} (Phạm vi: {r.MemoryScope} | Độ khớp: {matchPercent}%)");
            rsb.AppendLine(r.Content);
        }

        return new RobotChatResponse(rsb.ToString().TrimEnd(), "/memory");
    }

    private async Task<RobotChatResponse> HandleProactiveCommandAsync(CancellationToken cancellationToken)
    {
        var insights = await proactiveScanner!.GetActiveProactiveInsightsAsync(tenantContext.TenantId!.Value, cancellationToken);
        if (insights.Count == 0)
        {
            return new RobotChatResponse(
                "Agent Giám sát Chủ động (Autonomous Daemon):\n" +
                "Hiện tại không có rủi ro trễ hạn SLA hoặc tình trạng quá tải nhân sự nào được phát hiện trong hệ thống. Mọi thứ đang vận hành ổn định!",
                "/proactive");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Phát hiện {insights.Count} khuyến nghị từ Agent Giám sát Chủ động:\n");
        var cards = new List<RobotActionCard>();

        foreach (var insight in insights)
        {
            sb.AppendLine($"- {insight.Title} (Độ tin cậy: {Math.Round(insight.Confidence * 100)}%)");
            sb.AppendLine($"  {insight.Description}");
            if (insight.ObjectId.HasValue)
            {
                cards.Add(new RobotActionCard("VIEW_TASK", "Xem chi tiết", $"/tasks/{insight.ObjectId.Value}"));
            }
        }

        return new RobotChatResponse(sb.ToString().TrimEnd(), "/proactive", cards.Take(3).ToList());
    }

    private async Task<RobotChatResponse> HandleRememberCommandAsync(string message, CancellationToken cancellationToken)
    {
        var content = message;
        if (content.StartsWith("/remember", StringComparison.OrdinalIgnoreCase))
            content = content["/remember".Length..].Trim();
        else if (content.StartsWith("nhớ rằng", StringComparison.OrdinalIgnoreCase))
            content = content["nhớ rằng".Length..].Trim();
        else if (content.StartsWith("ghi nhớ:", StringComparison.OrdinalIgnoreCase))
            content = content["ghi nhớ:".Length..].Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            return new RobotChatResponse("Vui lòng nhập nội dung bạn muốn tôi ghi nhớ.\nVí dụ: `/remember Tôi thường phụ trách phê duyệt các đề xuất mua sắm dưới 20 triệu.`", "/remember");
        }
        if (content.Length > 500)
            throw new ApplicationFault(FaultKind.Validation, "AI.PREFERENCE_TOO_LONG", "Sở thích được ghi nhớ tối đa 500 ký tự.");

        var title = content.Length <= 40 ? content : content[..40] + "...";
        var meta = JsonSerializer.Serialize(new { userId = tenantContext.UserId, savedAt = clock.GetUtcNow() });

        if (learnedChoiceStore != null)
            await learnedChoiceStore.RecordChoiceAsync(tenantContext.TenantId!.Value, tenantContext.UserId,
                "preference", content, Truncate(content, 250), "PERSONAL", clock.GetUtcNow(), cancellationToken);

        if (vectorMemoryStore != null) await vectorMemoryStore.StoreMemoryAsync(
            tenantContext.TenantId!.Value,
            "USER",
            title,
            content,
            meta,
            cancellationToken);

        return new RobotChatResponse(
            $"Đã ghi nhớ vào hồ sơ cá nhân của bạn:\n\"{content}\"\n\nTôi sẽ tối ưu hóa các câu trả lời và đề xuất tiếp theo phù hợp với vai trò và thói quen của bạn.",
            "/remember");
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";

}
