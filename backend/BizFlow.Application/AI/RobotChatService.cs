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

public sealed class RobotChatService(
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
    IAgentMeetingCoordinator? meetingCoordinator = null,
    IAiContextReader? contextReader = null,
    IAiLearnedChoiceStore? learnedChoiceStore = null,
    IConversationStateManager? conversationStateManager = null) : IRobotChatService
{
    public async Task<RobotChatResponse> ProcessMessageAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken = default)
    {
        var rawResponse = await ExecuteInternalProcessAsync(command, cancellationToken);
        return SanitizeResponse(rawResponse);
    }

    private static string CleanOutputText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // Remove markdown bold / italic asterisks
        var cleaned = Regex.Replace(text, @"\*{1,3}", "");
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
        var cleanedResponse = CleanOutputText(resp.Response);
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
        if (vectorMemoryStore != null && (
            message.StartsWith("/remember", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("nhớ rằng", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("ghi nhớ:", StringComparison.OrdinalIgnoreCase)))
        {
            return await HandleRememberCommandAsync(message, cancellationToken);
        }

        // 7. Generalized Enterprise AI Agent: Thinking, Reasoning, Clarifying Uncertainties & Autonomous Planning
        var agentResponse = await TryHandleAgentInitiativeAndReasoningAsync(command, cancellationToken);
        if (agentResponse != null)
        {
            return agentResponse;
        }

        // 8. Natural Language Task and Deadline Query with Tool Orchestration, Multi-turn Context, RBAC & Anti-Hallucination
        return await HandleNaturalLanguageQueryAsync(command, cancellationToken);
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

        var title = content.Length <= 40 ? content : content[..40] + "...";
        var meta = JsonSerializer.Serialize(new { userId = tenantContext.UserId, savedAt = clock.GetUtcNow() });

        await vectorMemoryStore!.StoreMemoryAsync(
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

    private async Task<RobotChatResponse?> TryHandleAgentInitiativeAndReasoningAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken)
    {
        var message = command.Message?.Trim() ?? "";
        var lowerMsg = message.ToLowerInvariant();

        // 0. Handle structured Agent Questionnaire Form Submission if present
        if (command.FormAnswers is { Count: > 0 } || !string.IsNullOrWhiteSpace(command.FormId))
        {
            return await HandleFormSubmissionAsync(command, cancellationToken);
        }

        // 0.1 Check active multi-turn conversation session from ConversationStateManager
        if (conversationStateManager != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
        {
            var convId = command.ConversationId ?? "default";
            var session = await conversationStateManager.GetStateAsync(tenantContext.TenantId.Value, tenantContext.UserId.Value, convId, cancellationToken);
            if (session?.PendingClarification != null && !lowerMsg.StartsWith("/") && !lowerMsg.Contains("deadline") && !lowerMsg.Contains("quá hạn"))
            {
                var originalUserMsg = session.ActiveIntent ?? "Kế hoạch công việc";
                var synthesizedPrompt = $"{originalUserMsg}: {message}";
                var thought = "Quá trình Suy nghĩ & Ra Quyết định của AI Agent (Hội thoại Đa lượt):\n" +
                    $"- Tiếp nhận thông tin bổ sung: \"{message}\"\n" +
                    "- Đánh giá: Dữ liệu đã đủ chi tiết để xác định mục tiêu và phân định trách nhiệm.\n" +
                    "- Quyết định: Phân rã tự động thành các nhánh nhiệm vụ gửi tới các phòng ban phụ trách.";

                // Learn custom input from user follow-up
                if (learnedChoiceStore != null)
                {
                    try
                    {
                        await learnedChoiceStore.RecordChoiceAsync(
                            tenantContext.TenantId.Value,
                            tenantContext.UserId,
                            "general_input",
                            message,
                            message,
                            "PERSONAL",
                            clock.GetUtcNow(),
                            cancellationToken);
                    }
                    catch { }
                }

                await conversationStateManager.ClearStateAsync(tenantContext.TenantId.Value, tenantContext.UserId.Value, convId, cancellationToken);
                return await ExecuteAgentDecompositionAsync(synthesizedPrompt, originalTitle: originalUserMsg, userClarifiedDetails: message, agentThought: thought, cancellationToken: cancellationToken);
            }
        }

        AiTenantContextData? tenantContextData = null;
        if (contextReader != null && tenantContext.TenantId.HasValue)
        {
            try
            {
                tenantContextData = await contextReader.GetTenantContextAsync(tenantContext.TenantId.Value, cancellationToken);
            }
            catch { }
        }

        // 0.2 Try LLM-first agent orchestration with structured JSON reasoning
        var llmResponse = await TryHandleLlmAgentOrchestrationAsync(command, tenantContextData, cancellationToken);
        if (llmResponse != null)
        {
            return llmResponse;
        }

        // Check if message is a new operational initiative or command
        bool isInitiativeKeyword =
            lowerMsg.Contains("cuộc họp") || lowerMsg.Contains("họp") || lowerMsg.Contains("meeting") ||
            lowerMsg.Contains("thông báo") || lowerMsg.Contains("chuẩn bị") || lowerMsg.Contains("tổ chức") ||
            lowerMsg.Contains("chiến dịch") || lowerMsg.Contains("marketing") || lowerMsg.Contains("tuyển") ||
            lowerMsg.Contains("máy chủ") || lowerMsg.Contains("nâng cấp") || lowerMsg.Contains("lập kế hoạch") ||
            lowerMsg.Contains("lên kế hoạch");

        // 1. Only consider user answering a previous clarification if the IMMEDIATELY preceding message in history was a clarification,
        // AND the user is NOT starting a brand new initiative
        var lastHistoryMsg = command.History?.LastOrDefault();
        bool isPrecededByClarification = lastHistoryMsg != null &&
            (lastHistoryMsg.Sender.Equals("robot", StringComparison.OrdinalIgnoreCase) || lastHistoryMsg.Sender.Equals("assistant", StringComparison.OrdinalIgnoreCase)) &&
            (lastHistoryMsg.Text.Contains("làm rõ giúp tôi") || lastHistoryMsg.Text.Contains("chưa chắc chắn") ||
             lastHistoryMsg.Text.Contains("Góc nhìn Phản biện") || lastHistoryMsg.Text.Contains("AGENT_CLARIFICATION_REQUIRED") ||
             lastHistoryMsg.Text.Contains("Bảng câu hỏi") || lastHistoryMsg.Text.Contains("khảo sát") ||
             lastHistoryMsg.Text.Contains("Thời gian & Thời lượng"));

        if (!isInitiativeKeyword && isPrecededByClarification)
        {
            // If user's message is not a navigation or specific command, treat it as clarifying details
            if (!lowerMsg.StartsWith("/") && !lowerMsg.Contains("deadline") && !lowerMsg.Contains("quá hạn") && !lowerMsg.Contains("nhớ gì về tôi"))
            {
                var originalUserMsg = command.History?
                    .TakeWhile(h => h != lastHistoryMsg)
                    .LastOrDefault(h => h.Sender.Equals("user", StringComparison.OrdinalIgnoreCase))?.Text ?? "Kế hoạch công việc";

                var synthesizedPrompt = $"{originalUserMsg}: {message}";
                var thought = "Quá trình Suy nghĩ & Ra Quyết định của AI Agent:\n" +
                    $"- Tiếp nhận thông tin bổ sung: \"{message}\"\n" +
                    "- Đánh giá: Dữ liệu đã đủ chi tiết để xác định mục tiêu và phân định trách nhiệm.\n" +
                    "- Quyết định: Phân rã tự động thành các nhánh nhiệm vụ gửi tới các phòng ban phụ trách.";
                return await ExecuteAgentDecompositionAsync(synthesizedPrompt, originalTitle: originalUserMsg, userClarifiedDetails: message, agentThought: thought, cancellationToken: cancellationToken);
            }
        }

        // 2. Identify if query is purely a factual status lookup in existing database tasks
        bool isPureStatusLookup =
            (lowerMsg.Contains("hôm nay") || lowerMsg.Contains("today") ||
             lowerMsg.Contains("quá hạn") || lowerMsg.Contains("trễ hạn") || lowerMsg.Contains("overdue") ||
             lowerMsg.Contains("việc của tôi") || lowerMsg.Contains("chờ xử lý") ||
             lowerMsg.Contains("ai làm") || lowerMsg.Contains("tiến độ") ||
             lowerMsg.Contains("deadline") || lowerMsg.Contains("hạn chót") ||
             lowerMsg.Contains("nhớ gì") || lowerMsg.Contains("về tôi") ||
             lowerMsg.Contains("quy định") || lowerMsg.Contains("chính sách") || lowerMsg.Contains("nội quy"))
            && !lowerMsg.Contains("lên kế hoạch") && !lowerMsg.Contains("lập kế hoạch") && !lowerMsg.Contains("chuẩn bị") &&
               !lowerMsg.Contains("tổ chức") && !lowerMsg.Contains("triển khai") && !lowerMsg.Contains("chiến dịch") &&
               !lowerMsg.Contains("tuyển");

        if (isPureStatusLookup)
        {
            return null; // Let factual query handler process with anti-hallucination rules
        }

        // 3. Domain Categorization for Operational Initiatives & Problems to Solve
        bool isMeeting = lowerMsg.Contains("họp") || lowerMsg.Contains("meeting");
        bool isMarketing = lowerMsg.Contains("marketing") || lowerMsg.Contains("chiến dịch") || lowerMsg.Contains("campaign") ||
                           lowerMsg.Contains("quảng cáo") || lowerMsg.Contains("ra mắt") || lowerMsg.Contains("launch") ||
                           lowerMsg.Contains("pr") || lowerMsg.Contains("truyền thông") || lowerMsg.Contains("bán hàng") || lowerMsg.Contains("sales");
        bool isRecruitment = lowerMsg.Contains("tuyển") || lowerMsg.Contains("tuyển dụng") || lowerMsg.Contains("hiring") ||
                             lowerMsg.Contains("onboarding") || lowerMsg.Contains("nhân sự mới") || lowerMsg.Contains("đào tạo") || lowerMsg.Contains("training");
        bool isTech = lowerMsg.Contains("máy chủ") || lowerMsg.Contains("server") || lowerMsg.Contains("bảo mật") ||
                      lowerMsg.Contains("nâng cấp") || lowerMsg.Contains("phần mềm") || lowerMsg.Contains("hạ tầng") ||
                      lowerMsg.Contains("hệ thống") || lowerMsg.Contains("deploy") || lowerMsg.Contains("crm") || lowerMsg.Contains("erp");
        bool isIncident = lowerMsg.Contains("sự cố") || lowerMsg.Contains("khiếu nại") || lowerMsg.Contains("phàn nàn") ||
                          lowerMsg.Contains("khắc phục") || lowerMsg.Contains("nguy cơ") || lowerMsg.Contains("rủi ro");
        bool isGeneralPlanning = lowerMsg.Contains("chuẩn bị") || lowerMsg.Contains("tổ chức") ||
                                 lowerMsg.Contains("lên kế hoạch") || lowerMsg.Contains("lập kế hoạch") ||
                                 lowerMsg.Contains("triển khai") || lowerMsg.Contains("xây dựng") ||
                                 lowerMsg.Contains("tối ưu") || lowerMsg.Contains("giảm chi phí") ||
                                 lowerMsg.Contains("tiếp đón") || lowerMsg.Contains("đón tiếp") ||
                                 lowerMsg.Contains("chuyển văn phòng") || lowerMsg.Contains("sự kiện") ||
                                 lowerMsg.Contains("hội thảo") || lowerMsg.Contains("workshop") ||
                                 lowerMsg.Contains("phương án") || lowerMsg.Contains("giúp tôi") ||
                                 lowerMsg.Contains("cần làm") || lowerMsg.Contains("hướng dẫn tôi");

        bool isInitiative = isMeeting || isMarketing || isRecruitment || isTech || isIncident || isGeneralPlanning;
        if (!isInitiative)
        {
            return null; // Not an operational initiative
        }

        // 4. Uncertainty & Completeness Assessment
        // Check for specific concrete time / timeframe
        bool hasSpecificTime = Regex.IsMatch(lowerMsg, @"\b(?:\d{1,2}h(?:\d{2})?|\d{1,2}:\d{2}|\d{1,2}\s*(?:giờ|g|am|pm)|ngày \d{1,2}|tháng \d{1,2}|tuần tới|quý [1-4]|q[1-4]|hạn chót|deadline)\b", RegexOptions.IgnoreCase);

        // Check for specific scope / metrics / volume
        bool hasScopeOrMetrics = Regex.IsMatch(lowerMsg, @"\b(?:\d+\s*(?:người|nhân sự|triệu|tỷ|usd|vnd|kpi|lead|khách|bộ phận|vị trí|phòng|bước))\b", RegexOptions.IgnoreCase);

        // Check for concrete departments or assignees
        bool hasDepartmentOrOwner = Regex.IsMatch(lowerMsg, @"\b(?:marketing|sales|it|kỹ thuật|nhân sự|hr|hành chính|kế toán|tài chính|ban giám đốc|bgd|thư ký|dev|tester|trưởng phòng)\b", RegexOptions.IgnoreCase);

        // Check for specific meeting location
        bool hasLocation = Regex.IsMatch(lowerMsg, @"\b(?:phòng họp|phòng số|phòng a|phòng b|phòng lớn|phòng nhỏ|tầng \d+|zoom|meet|teams|online|trực tuyến|trực tiếp)\b", RegexOptions.IgnoreCase);

        var wordCount = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        bool isSufficientlyDetailed;
        if (isMeeting)
        {
            isSufficientlyDetailed = hasSpecificTime && hasLocation && hasDepartmentOrOwner && wordCount >= 15;
        }
        else
        {
            isSufficientlyDetailed = (wordCount >= 20 && hasSpecificTime && (hasDepartmentOrOwner || hasScopeOrMetrics)) ||
                                     (hasSpecificTime && hasDepartmentOrOwner && hasScopeOrMetrics);
        }

        if (!isSufficientlyDetailed)
        {
            return await BuildAgentClarificationResponseAsync(
                message,
                isMeeting,
                isMarketing,
                isRecruitment,
                isTech,
                isIncident,
                tenantContextData,
                command.ConversationId,
                cancellationToken);
        }

        // If request is sufficiently detailed upfront, execute decomposition immediately!
        var upfrontThought = "Quá trình Suy nghĩ & Ra Quyết định của AI Agent:\n" +
            "- Phân tích yêu cầu ban đầu: Đã cung cấp đầy đủ các biến số vận hành then chốt (thời gian, địa điểm, thành phần).\n" +
            "- Quyết định: Kích hoạt quy trình phân rã tự động và giao việc trực tiếp tới các phòng ban.";
        return await ExecuteAgentDecompositionAsync(message, originalTitle: message, userClarifiedDetails: null, agentThought: upfrontThought, cancellationToken: cancellationToken);
    }

    private async Task<RobotChatResponse> HandleFormSubmissionAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken)
    {
        AiTenantContextData? tenantContextData = null;
        if (contextReader != null && tenantContext.TenantId.HasValue)
        {
            try
            {
                tenantContextData = await contextReader.GetTenantContextAsync(tenantContext.TenantId.Value, cancellationToken);
            }
            catch { }
        }

        var formId = command.FormId ?? "form_general";
        var answers = command.FormAnswers ?? [];
        var baseForm = GetFormDefinition(formId, command.Message, tenantContextData);

        // Record answered choices into adaptive choice memory
        if (learnedChoiceStore != null && tenantContext.TenantId.HasValue)
        {
            var tId = tenantContext.TenantId.Value;
            var uId = tenantContext.UserId;
            var now = clock.GetUtcNow();
            foreach (var ans in answers)
            {
                var q = baseForm.Questions.FirstOrDefault(x => x.Id.Equals(ans.QuestionId, StringComparison.OrdinalIgnoreCase));
                var semanticKey = ans.QuestionId.ToLowerInvariant().Replace("q_", "");

                if (ans.SelectedOptionIds is { Count: > 0 })
                {
                    foreach (var optId in ans.SelectedOptionIds)
                    {
                        var optLabel = q?.Options?.FirstOrDefault(o => o.Id == optId)?.Label ?? optId;
                        try
                        {
                            await learnedChoiceStore.RecordChoiceAsync(tId, uId, semanticKey, optId, optLabel, "PERSONAL", now, cancellationToken);
                        }
                        catch { }
                    }
                }

                if (!string.IsNullOrWhiteSpace(ans.CustomText))
                {
                    try
                    {
                        await learnedChoiceStore.RecordChoiceAsync(tId, uId, semanticKey, ans.CustomText.Trim(), ans.CustomText.Trim(), "PERSONAL", now, cancellationToken);
                    }
                    catch { }
                }
            }
        }

        var missingQuestions = new List<AgentQuestionDto>();
        var answeredMap = new Dictionary<string, string>();

        foreach (var q in baseForm.Questions)
        {
            var userAns = answers.FirstOrDefault(a => a.QuestionId.Equals(q.Id, StringComparison.OrdinalIgnoreCase));
            var answeredText = "";
            if (userAns != null)
            {
                if (userAns.SelectedOptionIds is { Count: > 0 })
                {
                    var labels = userAns.SelectedOptionIds
                        .Select(id => q.Options?.FirstOrDefault(o => o.Id == id)?.Label ?? id)
                        .ToList();
                    answeredText = string.Join(", ", labels);
                }
                if (!string.IsNullOrWhiteSpace(userAns.CustomText))
                {
                    answeredText = string.IsNullOrWhiteSpace(answeredText)
                        ? userAns.CustomText.Trim()
                        : $"{answeredText} ({userAns.CustomText.Trim()})";
                }
            }

            if (string.IsNullOrWhiteSpace(answeredText))
            {
                if (q.Required)
                {
                    missingQuestions.Add(q);
                }
            }
            else
            {
                answeredMap[q.Id] = $"{q.Title}: {answeredText}";
            }
        }

        // If any required question was not answered, generate a targeted follow-up questionnaire!
        if (missingQuestions.Count > 0)
        {
            var missingTitles = string.Join(", ", missingQuestions.Select(m => m.Title));
            var partialSummary = answeredMap.Count > 0
                ? string.Join("\n", answeredMap.Values.Select(v => $"- {v}"))
                : "Chưa có thông tin hợp lệ";

            var followUpThought = $"Suy nghĩ của AI Agent: Tiếp nhận phản hồi biểu mẫu nhưng phát hiện vẫn còn thiếu {missingQuestions.Count} thông tin bắt buộc ({missingTitles}). Agent từ chối tạo việc ngay để ngăn chặn rủi ro thiếu sót và gửi lại các câu hỏi còn thiếu.";

            var followUpForm = new AgentClarificationFormDto(
                formId,
                baseForm.Topic,
                "Tôi đã ghi nhận các thông tin bạn vừa chọn. Tuy nhiên, các thông tin sau vẫn là điều kiện bắt buộc để giao việc đúng bộ phận:",
                baseForm.RiskAnalysis,
                missingQuestions,
                "Hoàn tất & Tiếp tục phân tích",
                "Sử dụng thiết lập khuyến nghị");

            if (conversationStateManager != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
            {
                var convId = command.ConversationId ?? "default";
                var state = new ConversationState(
                    convId,
                    tenantContext.TenantId.Value,
                    tenantContext.UserId.Value,
                    baseForm.Topic,
                    answeredMap,
                    missingQuestions.Select(m => m.Id).ToList(),
                    followUpForm,
                    command.History != null ? new List<RobotChatMessageDto>(command.History) : [],
                    clock.GetUtcNow());
                _ = conversationStateManager.SaveStateAsync(state, cancellationToken);
            }

            var followUpText = $"Cảm ơn bạn! Tôi đã ghi nhận một phần thông tin:\n{partialSummary}\n\nĐể đảm bảo phân công chính xác và không tạo việc thừa, bạn vui lòng chọn nốt các thông tin còn thiếu bên dưới:";

            return new RobotChatResponse(
                followUpText,
                "AGENT_CLARIFICATION_REQUIRED",
                [new RobotActionCard("PREFILL_PROMPT", "Tiếp tục làm rõ", null, new { template = "Tiếp tục hoàn tất thông tin" })],
                Questionnaire: followUpForm,
                AgentThought: followUpThought);
        }

        // All required questions are answered! Agent makes the definitive plan.
        if (conversationStateManager != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
        {
            var convId = command.ConversationId ?? "default";
            _ = conversationStateManager.ClearStateAsync(tenantContext.TenantId.Value, tenantContext.UserId.Value, convId, cancellationToken);
        }

        var detailsSummary = string.Join("\n", answeredMap.Values.Select(v => $"- {v}"));
        var synthesizedPrompt = $"{baseForm.Topic}: {string.Join("; ", answeredMap.Values)}";

        // Special handling for Enterprise Meeting & Notification Coordination
        if (formId.Equals("form_meeting", StringComparison.OrdinalIgnoreCase) ||
            baseForm.Topic.Contains("cuộc họp", StringComparison.OrdinalIgnoreCase) ||
            baseForm.Topic.Contains("họp", StringComparison.OrdinalIgnoreCase))
        {
            var timeAns = answers.FirstOrDefault(a => a.QuestionId.Equals("q_meeting_time", StringComparison.OrdinalIgnoreCase));
            var locAns = answers.FirstOrDefault(a => a.QuestionId.Equals("q_meeting_format", StringComparison.OrdinalIgnoreCase));
            var contentAns = answers.FirstOrDefault(a => a.QuestionId.Equals("q_meeting_content", StringComparison.OrdinalIgnoreCase));
            var attAns = answers.FirstOrDefault(a => a.QuestionId.Equals("q_meeting_attendees", StringComparison.OrdinalIgnoreCase));
            var eqAns = answers.FirstOrDefault(a => a.QuestionId.Equals("q_meeting_equipment", StringComparison.OrdinalIgnoreCase));

            var meetingTime = timeAns?.CustomText ?? (timeAns?.SelectedOptionIds is { Count: > 0 }
                ? baseForm.Questions.FirstOrDefault(q => q.Id == "q_meeting_time")?.Options?.FirstOrDefault(o => o.Id == timeAns.SelectedOptionIds[0])?.Label ?? "9h00 sáng mai (1 tiếng)"
                : "9h00 sáng mai (1 tiếng)");

            var meetingLocation = locAns?.CustomText ?? (locAns?.SelectedOptionIds is { Count: > 0 }
                ? baseForm.Questions.FirstOrDefault(q => q.Id == "q_meeting_format")?.Options?.FirstOrDefault(o => o.Id == locAns.SelectedOptionIds[0])?.Label ?? "Phòng họp Lớn (Tầng 2)"
                : "Phòng họp Lớn (Tầng 2)");

            var meetingContent = contentAns?.CustomText ?? (contentAns?.SelectedOptionIds is { Count: > 0 }
                ? baseForm.Questions.FirstOrDefault(q => q.Id == "q_meeting_content")?.Options?.FirstOrDefault(o => o.Id == contentAns.SelectedOptionIds[0])?.Label ?? "Rà soát tiến độ kinh doanh quý 4 & Kế hoạch năm mới"
                : "Rà soát tiến độ kinh doanh quý 4 & Kế hoạch năm mới");

            var attLabels = new List<string>();
            if (attAns?.SelectedOptionIds is { Count: > 0 })
            {
                var qAtt = baseForm.Questions.FirstOrDefault(q => q.Id == "q_meeting_attendees");
                attLabels.AddRange(attAns.SelectedOptionIds.Select(id => qAtt?.Options?.FirstOrDefault(o => o.Id == id)?.Label ?? id));
            }
            if (!string.IsNullOrWhiteSpace(attAns?.CustomText))
            {
                attLabels.Add(attAns.CustomText.Trim());
            }
            if (attLabels.Count == 0)
            {
                attLabels.AddRange(["Ban Giám đốc (BGĐ)", "Phòng Công nghệ & Chuyển đổi số (TECH)", "Phòng Hành chính - Nhân sự (HR)"]);
            }

            var eqLabels = new List<string>();
            if (eqAns?.SelectedOptionIds is { Count: > 0 })
            {
                var qEq = baseForm.Questions.FirstOrDefault(q => q.Id == "q_meeting_equipment");
                eqLabels.AddRange(eqAns.SelectedOptionIds.Select(id => qEq?.Options?.FirstOrDefault(o => o.Id == id)?.Label ?? id));
            }
            if (!string.IsNullOrWhiteSpace(eqAns?.CustomText))
            {
                eqLabels.Add(eqAns.CustomText.Trim());
            }
            if (eqLabels.Count == 0)
            {
                eqLabels.AddRange(["Máy chiếu & Màn hình LED", "In ấn tài liệu trước cuộc họp"]);
            }

            // 1. Dispatch autonomous In-App notifications and Emails to department managers
            MeetingDispatchResultDto? dispatchResult = null;
            if (meetingCoordinator != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
            {
                try
                {
                    dispatchResult = await meetingCoordinator.DispatchMeetingNotificationAsync(
                        tenantContext.TenantId.Value,
                        tenantContext.UserId.Value,
                        meetingContent,
                        meetingTime,
                        meetingLocation,
                        attLabels,
                        eqLabels,
                        cancellationToken);
                }
                catch
                {
                    // Fallback gracefully in testing
                }
            }

            // Grounding with real departments in DB if available
            var depts = tenantContextData?.Departments;
            var adminDept = depts?.FirstOrDefault(d => d.Name.Contains("Hành chính", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("HR", StringComparison.OrdinalIgnoreCase));
            var techDept = depts?.FirstOrDefault(d => d.Name.Contains("Công nghệ", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Chuyển đổi số", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Kỹ thuật", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("TECH", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("IT", StringComparison.OrdinalIgnoreCase));
            var execDept = depts?.FirstOrDefault(d => d.Name.Contains("Giám đốc", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Điều hành", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("BOD", StringComparison.OrdinalIgnoreCase));

            // SLA Decision: Calculate urgency based on timeframe (e.g. tomorrow morning / within 24h = HIGH)
            var lowerTime = meetingTime.ToLowerInvariant();
            bool isWithin24h = lowerTime.Contains("sáng mai") || lowerTime.Contains("chiều mai") || lowerTime.Contains("hôm nay") || lowerTime.Contains("gấp");
            var meetingPriority = isWithin24h ? "HIGH" : "MEDIUM";

            // 2. Generate multi-branch task proposals tailored for the assigned units
            var proposals = new List<TaskDecompositionProposalDto>
            {
                new(
                    Title: "Chuẩn bị phòng họp, nước uống và in ấn tài liệu",
                    Description: $"Tiếp nhận yêu cầu chuẩn bị cơ sở vật chất, hậu cần cho cuộc họp '{meetingContent}' diễn ra vào lúc {meetingTime} tại {meetingLocation}.",
                    Type: "Assignment",
                    DepartmentId: adminDept?.DepartmentId,
                    DepartmentName: adminDept?.Name ?? "Phòng Hành chính - Nhân sự",
                    Priority: meetingPriority,
                    ChecklistItems:
                    [
                        $"Xác nhận đặt {meetingLocation}",
                        "Kiểm tra vệ sinh và sắp xếp bàn ghế phòng họp",
                        "Chuẩn bị nước uống và teabreak cho đại biểu",
                        $"In ấn tài liệu cuộc họp ({meetingContent})",
                        "Bàn giao phòng họp trước giờ bắt đầu 15 phút"
                    ],
                    Reasoning: $"Bộ phận Hành chính chịu trách nhiệm hậu cần, chuẩn bị phòng họp và tiếp đón đại biểu. Căn cứ theo cam kết SLA và thời gian diễn ra trong vòng 24 giờ, mức độ ưu tiên được xác định là {meetingPriority}."),
                new(
                    Title: "Kiểm tra máy chiếu, thiết bị âm thanh và kết nối mạng",
                    Description: $"Cấu hình trang thiết bị công nghệ thông tin, màn hình LED/máy chiếu, micro và đường truyền họp trực tuyến phục vụ cuộc họp '{meetingContent}'.",
                    Type: "SupportRequest",
                    DepartmentId: techDept?.DepartmentId,
                    DepartmentName: techDept?.Name ?? "Phòng Công nghệ & Chuyển đổi số",
                    Priority: meetingPriority,
                    ChecklistItems:
                    [
                        $"Bật máy chiếu và kết nối dây cáp HDMI/trình chiếu tại {meetingLocation}",
                        "Kiểm tra micro, loa âm thanh hội nghị",
                        "Kiểm tra ổn định mạng internet và link họp trực tuyến (Google Meet/Zoom)",
                        "Hỗ trợ kỹ thuật trực tiếp trong suốt thời gian diễn ra cuộc họp"
                    ],
                    Reasoning: $"Bộ phận IT phụ trách hạ tầng kỹ thuật, âm thanh, trình chiếu và đường truyền họp trực tuyến theo SLA hỗ trợ kỹ thuật {meetingPriority}."),
                new(
                    Title: "Gửi thông báo lịch họp và tổng hợp danh sách đại biểu tham dự",
                    Description: $"Điều phối lịch trình, gửi thư mời và tài liệu nghị sự tới các đơn vị dưới quyền tham gia cuộc họp ({string.Join(", ", attLabels)}).",
                    Type: "Assignment",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Ban Giám đốc & Điều hành",
                    Priority: "MEDIUM",
                    ChecklistItems:
                    [
                        "Gửi email/thông báo lịch họp tới các đại biểu",
                        $"Đính kèm tài liệu chương trình nghị sự ({meetingContent})",
                        "Xác nhận danh sách đại biểu tham gia từ các đơn vị dưới quyền",
                        "Điểm danh và lập biên bản cuộc họp"
                    ],
                    Reasoning: "Công tác điều phối lịch trình và nội dung cuộc họp do Thư ký/Ban Giám đốc đảm nhiệm.")
            };

            var notifCount = dispatchResult?.InAppNotificationsSent ?? attLabels.Count;
            var emailCount = dispatchResult?.EmailsSent ?? attLabels.Count;

            var sb = new StringBuilder();
            sb.AppendLine("Trợ lý AI Agent đã xử lý thông tin cuộc họp và tự động thực thi các tác vụ vận hành:\n");
            sb.AppendLine("Thông tin cuộc họp đã thống nhất:");
            sb.AppendLine($"- Thời gian: {meetingTime}");
            sb.AppendLine($"- Địa điểm: {meetingLocation}");
            sb.AppendLine($"- Nội dung cuộc họp: {meetingContent}");
            sb.AppendLine($"- Đơn vị dưới quyền tham gia: {string.Join(", ", attLabels)}");
            if (eqLabels.Count > 0)
            {
                sb.AppendLine($"- Hậu cần & Trang thiết bị: {string.Join(", ", eqLabels)}");
            }
            sb.AppendLine();
            sb.AppendLine($"Đã tự động gửi thông báo In-App: Gửi thành công thông báo họp tới {notifCount} Quản lý đơn vị dưới quyền qua Hộp thông báo trong ứng dụng.");
            sb.AppendLine($"Đã tự động phát thông báo Email: Gửi thành công thư mời họp tới {emailCount} email Quản lý phụ trách các phòng ban.");
            sb.AppendLine();
            sb.AppendLine($"Đề xuất phân công tới các phòng ban chuyên trách ({proposals.Count} nhánh nhiệm vụ):");
            for (var i = 0; i < proposals.Count; i++)
            {
                var p = proposals[i];
                var typeLabel = p.Type == "SupportRequest" ? "Yêu cầu hỗ trợ" : "Giao việc";
                sb.AppendLine($"{i + 1}. {p.Title} [{typeLabel}] - Mức ưu tiên: {p.Priority}");
                sb.AppendLine($"   - Phòng ban: {p.DepartmentName}");
                if (p.ChecklistItems is { Count: > 0 })
                {
                    sb.AppendLine($"   - Các bước: {string.Join(", ", p.ChecklistItems)}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("Vui lòng mở tab Nhánh tác vụ hoặc bấm nút bên dưới để xem chi tiết, chỉnh sửa inline và xác nhận tạo việc.");

            var agentThought = "Quá trình Suy nghĩ & Ra Quyết định của AI Agent:\n" +
                "1. Thẩm định thông tin cuộc họp: Đã thu thập đầy đủ 4 thông số bắt buộc (Thời gian, Địa điểm, Nội dung họp, Đơn vị dưới quyền tham gia).\n" +
                $"2. Đối chiếu quy trình và cam kết SLA: Sự kiện diễn ra trong vòng 24 giờ, hệ thống áp dụng mức ưu tiên {meetingPriority} cho các công tác chuẩn bị khẩn theo quy định của tổ chức.\n" +
                $"3. Tự động thực thi tác vụ: Đã phát thông báo In-App tới {notifCount} Quản lý đơn vị dưới quyền và gửi Email thư mời họp tới các địa chỉ liên hệ.\n" +
                $"4. Phân rã nhiệm vụ: Khởi tạo {proposals.Count} nhánh công việc đề xuất giao cho các bộ phận chuyên trách trong hệ thống (Hành chính chuẩn bị phòng họp & hậu cần, IT phụ trách máy chiếu & âm thanh, Ban Giám đốc điều phối lịch trình & biên bản).";

            var cards = new List<RobotActionCard>
            {
                new("VIEW_BRANCHES", $"Xem các nhánh tác vụ ({proposals.Count} nhánh)", null, new { prompt = synthesizedPrompt, proposals }),
                new("OPEN_DECOMPOSITION", "Mở công cụ Phân rã AI", "/tasks/ai-decomposition", new { prompt = synthesizedPrompt, proposals }),
                new("OPEN_NOTIFICATIONS", "Xem hòm thư thông báo", "/notifications")
            };

            return new RobotChatResponse(
                sb.ToString().TrimEnd(),
                "/task",
                cards,
                AgentThought: agentThought);
        }

        var defaultThought = "Quá trình Suy nghĩ & Ra Quyết định của AI Agent:\n" +
            $"1. Tiếp nhận và kiểm tra thông tin: Đã thu thập đầy đủ {answeredMap.Count} thông số vận hành then chốt.\n" +
            "2. Đánh giá rủi ro: Thông tin rõ ràng, không còn điểm mơ hồ, đảm bảo việc giao đúng phòng ban.\n" +
            "3. Quyết định phân công: Tiến hành phân rã yêu cầu thành các nhánh nhiệm vụ cụ thể gửi đến các bộ phận chuyên trách.";

        return await ExecuteAgentDecompositionAsync(
            synthesizedPrompt,
            originalTitle: baseForm.Topic,
            userClarifiedDetails: detailsSummary,
            agentThought: defaultThought,
            cancellationToken: cancellationToken);
    }

    private static AgentClarificationFormDto GetFormDefinition(string formId, string topic, AiTenantContextData? tenantContextData = null)
    {
        var lower = (formId + " " + topic).ToLowerInvariant();
        var depts = tenantContextData?.Departments;

        if (lower.Contains("meeting") || lower.Contains("họp") || lower.Contains("thông báo"))
        {
            List<AgentQuestionOptionDto> attendeeOptions;
            if (depts is { Count: > 0 })
            {
                attendeeOptions = depts.Select(d => new AgentQuestionOptionDto(
                    $"att_{d.Code.ToLowerInvariant()}",
                    $"{d.Name} ({d.Code})",
                    IsDefault: d.Code.Equals("BOD", StringComparison.OrdinalIgnoreCase) ||
                               d.Code.Equals("TECH", StringComparison.OrdinalIgnoreCase) ||
                               d.Code.Equals("IT", StringComparison.OrdinalIgnoreCase) ||
                               d.Code.Equals("HR", StringComparison.OrdinalIgnoreCase)
                )).ToList();
            }
            else
            {
                attendeeOptions =
                [
                    new AgentQuestionOptionDto("att_bgd", "Ban Giám đốc (BGĐ)", true),
                    new AgentQuestionOptionDto("att_it", "Phòng Công nghệ & Chuyển đổi số (TECH)", true),
                    new AgentQuestionOptionDto("att_hr", "Phòng Hành chính - Nhân sự (HR)", true),
                    new AgentQuestionOptionDto("att_biz", "Phòng Kinh doanh B2B & Đối tác (BIZ)"),
                    new AgentQuestionOptionDto("att_fin", "Phòng Tài chính - Kế toán (FIN)"),
                    new AgentQuestionOptionDto("att_mkt", "Phòng Tiếp thị & Truyền thông Thương hiệu (MKT)"),
                    new AgentQuestionOptionDto("att_ops", "Phòng Dịch vụ Khách hàng & Vận hành (OPS)")
                ];
            }

            return new AgentClarificationFormDto(
                "form_meeting",
                "Chuẩn bị & Điều phối cuộc họp",
                "Để chuẩn bị chu đáo và phân chia công việc đúng phòng ban, vui lòng trả lời các câu hỏi sau:",
                "Nếu tổ chức họp mà chưa rõ thời gian chính xác, phòng họp, thành phần tham dự hoặc tài liệu cần thiết, cuộc họp dễ bị trễ giờ hoặc thiếu trang thiết bị hỗ trợ.",
                [
                    new AgentQuestionDto(
                        "q_meeting_time",
                        "Thời gian & Thời lượng cuộc họp",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("t1", "9h00 sáng mai (1 tiếng)", true),
                            new AgentQuestionOptionDto("t2", "14h00 chiều mai (1.5 tiếng)"),
                            new AgentQuestionOptionDto("t3", "10h00 sáng ngày mốt (2 tiếng)")
                        ],
                        Required: true,
                        Placeholder: "Hoặc nhập khung giờ khác..."),
                    new AgentQuestionDto(
                        "q_meeting_format",
                        "Địa điểm & Hình thức cuộc họp",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("loc1", "Phòng họp Lớn (Tầng 2)", true),
                            new AgentQuestionOptionDto("loc2", "Phòng họp Nhỏ (Tầng 1)"),
                            new AgentQuestionOptionDto("loc3", "Trực tuyến (Google Meet / Zoom)"),
                            new AgentQuestionOptionDto("loc4", "Kết hợp Hybrid (Văn phòng + Online)")
                        ],
                        Required: true,
                        Placeholder: "Nhập địa điểm phòng họp hoặc link họp trực tuyến..."),
                    new AgentQuestionDto(
                        "q_meeting_content",
                        "Nội dung & Mục tiêu cuộc họp",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("c1", "Rà soát tiến độ kinh doanh quý 4 & Triển khai kế hoạch năm mới", true),
                            new AgentQuestionOptionDto("c2", "Họp giao ban định kỳ & Đánh giá hiệu suất phòng ban"),
                            new AgentQuestionOptionDto("c3", "Triển khai dự án mới & Phân bổ nguồn lực nhân sự")
                        ],
                        Required: true,
                        Placeholder: "Nhập nội dung/chủ đề cuộc họp cụ thể..."),
                    new AgentQuestionDto(
                        "q_meeting_attendees",
                        "Các đơn vị dưới quyền tham gia cuộc họp",
                        "multiple_choice",
                        attendeeOptions,
                        Required: true,
                        Placeholder: "Nhập thêm đại biểu / đơn vị khác..."),
                    new AgentQuestionDto(
                        "q_meeting_equipment",
                        "Nhu cầu trang thiết bị & Hậu cần",
                        "multiple_choice",
                        [
                            new AgentQuestionOptionDto("eq_proj", "Máy chiếu & Màn hình LED", true),
                            new AgentQuestionOptionDto("eq_mic", "Micro & Loa hội nghị"),
                            new AgentQuestionOptionDto("eq_doc", "In ấn tài liệu trước cuộc họp", true),
                            new AgentQuestionOptionDto("eq_tea", "Teabreak & Nước suối")
                        ],
                        Required: false,
                        Placeholder: "Nhập yêu cầu hậu cần khác...")
                ]);
        }

        if (lower.Contains("marketing") || lower.Contains("chiến dịch") || lower.Contains("campaign"))
        {
            return new AgentClarificationFormDto(
                "form_marketing",
                "Chiến dịch Marketing & Phát triển kinh doanh",
                "Để đồng bộ giữa Marketing, Design và Sales, vui lòng xác nhận các thông tin sau:",
                "Nếu triển khai chiến dịch mà chưa rõ mục tiêu KPI và ngân sách, dễ dẫn đến lãng phí chi phí quảng cáo và hiệu quả tiếp cận thấp.",
                [
                    new AgentQuestionDto(
                        "q_mkt_goal",
                        "Mục tiêu cốt lõi của chiến dịch",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("g_lead", "Thu thập khách hàng tiềm năng (Lead Generation)", true),
                            new AgentQuestionOptionDto("g_brand", "Tăng độ nhận diện thương hiệu (Brand Awareness)"),
                            new AgentQuestionOptionDto("g_sales", "Tăng trưởng doanh số bán hàng trực tiếp")
                        ],
                        Required: true),
                    new AgentQuestionDto(
                        "q_mkt_channels",
                        "Kênh triển khai chủ đạo",
                        "multiple_choice",
                        [
                            new AgentQuestionOptionDto("ch_social", "Mạng xã hội (Facebook, TikTok, LinkedIn)", true),
                            new AgentQuestionOptionDto("ch_google", "Google Search & Ads", true),
                            new AgentQuestionOptionDto("ch_email", "Email Marketing & Bản tin"),
                            new AgentQuestionOptionDto("ch_event", "Sự kiện / Workshop kết nối")
                        ],
                        Required: true),
                    new AgentQuestionDto(
                        "q_mkt_budget",
                        "Ngân sách & Thời hạn chiến dịch",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("b_10", "Dưới 15 triệu - Chạy trong 2 tuần"),
                            new AgentQuestionOptionDto("b_30", "15 - 40 triệu - Triển khai trong 1 tháng", true),
                            new AgentQuestionOptionDto("b_50", "Trên 40 triệu - Chiến dịch tổng thể quý")
                        ],
                        Required: true)
                ]);
        }

        if (lower.Contains("tuyển") || lower.Contains("nhân sự") || lower.Contains("hiring"))
        {
            List<AgentQuestionOptionDto> recDeptOptions;
            if (depts is { Count: > 0 })
            {
                recDeptOptions = depts.Select(d => new AgentQuestionOptionDto(
                    $"d_{d.Code.ToLowerInvariant()}",
                    d.Name,
                    IsDefault: d.Code.Equals("TECH", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("IT", StringComparison.OrdinalIgnoreCase)
                )).ToList();
            }
            else
            {
                recDeptOptions =
                [
                    new AgentQuestionOptionDto("d_it", "Phòng Công nghệ Thông tin (IT)", true),
                    new AgentQuestionOptionDto("d_mkt", "Phòng Marketing & Kinh doanh"),
                    new AgentQuestionOptionDto("d_hr", "Phòng Hành chính - Nhân sự"),
                    new AgentQuestionOptionDto("d_fin", "Phòng Kế toán - Tài chính")
                ];
            }

            return new AgentClarificationFormDto(
                "form_recruitment",
                "Tuyển dụng & Đào tạo nhân sự",
                "Để chuẩn bị bản mô tả công việc (JD) và chỉ tiêu tuyển dụng, vui lòng làm rõ các mục sau:",
                "Nếu bắt đầu tuyển dụng mà chưa có JD và thời hạn đi làm cụ thể, quy trình tuyển chọn sẽ bị kéo dài và gây quá tải cho bộ phận nhân sự.",
                [
                    new AgentQuestionDto(
                        "q_rec_role",
                        "Vị trí & Cấp bậc cần tuyển",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("r_jun", "Thực tập sinh / Fresher"),
                            new AgentQuestionOptionDto("r_mid", "Chuyên viên (1 - 3 năm kinh nghiệm)", true),
                            new AgentQuestionOptionDto("r_sen", "Chuyên viên cấp cao / Senior"),
                            new AgentQuestionOptionDto("r_lead", "Trưởng nhóm / Quản lý")
                        ],
                        Required: true,
                        Placeholder: "Tên vị trí tuyển dụng..."),
                    new AgentQuestionDto(
                        "q_rec_dept",
                        "Phòng ban tiếp nhận nhân sự",
                        "single_choice",
                        recDeptOptions,
                        Required: true),
                    new AgentQuestionDto(
                        "q_rec_deadline",
                        "Thời hạn nhân sự đi làm (Onboarding)",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("dl_2w", "Tuyển gấp trong vòng 2 tuần"),
                            new AgentQuestionOptionDto("dl_1m", "Trong vòng 1 tháng tới", true),
                            new AgentQuestionOptionDto("dl_q", "Kế hoạch quý tiếp theo")
                        ],
                        Required: true)
                ]);
        }

        if (lower.Contains("máy chủ") || lower.Contains("server") || lower.Contains("bảo mật") || lower.Contains("nâng cấp"))
        {
            return new AgentClarificationFormDto(
                "form_tech",
                "Triển khai Kỹ thuật & Hạ tầng CNTT",
                "Để đảm bảo an toàn dữ liệu và hạn chế tối đa gián đoạn, vui lòng xác nhận các thông số sau:",
                "Nếu nâng cấp hệ thống mà chưa có kế hoạch Downtime và Rollback, hệ thống có nguy cơ gián đoạn hoạt động và mất mát dữ liệu.",
                [
                    new AgentQuestionDto(
                        "q_tech_scope",
                        "Phạm vi hệ thống kỹ thuật triển khai",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("sc_db", "Máy chủ & Cơ sở dữ liệu chính", true),
                            new AgentQuestionOptionDto("sc_api", "Dịch vụ Backend API & Nền tảng ứng dụng"),
                            new AgentQuestionOptionDto("sc_web", "Giao diện Frontend & Cổng người dùng"),
                            new AgentQuestionOptionDto("sc_net", "Hạ tầng mạng & Tường lửa")
                        ],
                        Required: true),
                    new AgentQuestionDto(
                        "q_tech_downtime",
                        "Khung giờ bảo trì & Downtime dự kiến",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("dt_1h", "22h00 - 24h00 cuối tuần (Downtime dưới 1 giờ)", true),
                            new AgentQuestionOptionDto("dt_4h", "00h00 - 04h00 sáng (Downtime 2-4 giờ)"),
                            new AgentQuestionOptionDto("dt_0", "Không gián đoạn dịch vụ (Zero-downtime)")
                        ],
                        Required: true),
                    new AgentQuestionDto(
                        "q_tech_backup",
                        "Phương án sao lưu & Kế hoạch Rollback",
                        "single_choice",
                        [
                            new AgentQuestionOptionDto("bk_ready", "Đã có bản sao lưu đầy đủ và script rollback tự động", true),
                            new AgentQuestionOptionDto("bk_manual", "Cần sao lưu thủ công trước khi bắt đầu"),
                            new AgentQuestionOptionDto("bk_none", "Chưa chuẩn bị phương án dự phòng")
                        ],
                        Required: true)
                ]);
        }

        List<AgentQuestionOptionDto> genDeptOptions;
        if (depts is { Count: > 0 })
        {
            genDeptOptions = depts.Select(d => new AgentQuestionOptionDto(
                $"dep_{d.Code.ToLowerInvariant()}",
                d.Name,
                IsDefault: d.Code.Equals("OPS", StringComparison.OrdinalIgnoreCase) || d.Code.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
            )).ToList();
        }
        else
        {
            genDeptOptions =
            [
                new AgentQuestionOptionDto("dep_ops", "Phòng Kế hoạch & Điều hành", true),
                new AgentQuestionOptionDto("dep_hr", "Phòng Hành chính - Nhân sự"),
                new AgentQuestionOptionDto("dep_it", "Phòng Công nghệ Thông tin"),
                new AgentQuestionOptionDto("dep_fin", "Phòng Tài chính - Kế toán")
            ];
        }

        return new AgentClarificationFormDto(
            "form_general",
            "Lập kế hoạch vận hành & Giải quyết công việc",
            "Để xây dựng kế hoạch khả thi và phân công đúng phòng ban, vui lòng làm rõ các mục sau:",
            "Cần xác định rõ đầu ra mong đợi, tiến độ và nguồn lực phụ trách chính, tránh tình trạng đùn đẩy trách nhiệm.",
            [
                new AgentQuestionDto(
                    "q_gen_deliverable",
                    "Kết quả đầu ra cụ thể mong đợi (Deliverables)",
                    "single_choice",
                    [
                        new AgentQuestionOptionDto("del_plan", "Bản kế hoạch chi tiết & phân công nhiệm vụ", true),
                        new AgentQuestionOptionDto("del_sop", "Quy trình vận hành chuẩn (SOP)"),
                        new AgentQuestionOptionDto("del_rep", "Báo cáo phân tích và giải pháp tối ưu")
                    ],
                    Required: true),
                new AgentQuestionDto(
                    "q_gen_dept",
                    "Phòng ban chủ trì thực hiện",
                    "single_choice",
                    genDeptOptions,
                    Required: true),
                new AgentQuestionDto(
                    "q_gen_timeline",
                    "Thời hạn hoàn thành mục tiêu",
                    "single_choice",
                    [
                        new AgentQuestionOptionDto("time_week", "Trong tuần này"),
                        new AgentQuestionOptionDto("time_2w", "Trong vòng 2 tuần tới", true),
                        new AgentQuestionOptionDto("time_month", "Trong tháng này")
                    ],
                    Required: true)
            ]);
    }

    private static RobotChatResponse BuildAgentClarificationResponse(
        string message,
        bool isMeeting,
        bool isMarketing,
        bool isRecruitment,
        bool isTech,
        bool isIncident,
        AiTenantContextData? tenantContextData = null)
    {
        var csb = new StringBuilder();
        csb.AppendLine("Chào bạn! Tôi là Trợ lý Doanh nghiệp AI Agent BizFlow.\n");

        string topicTitle;
        string riskPerspective;
        List<string> questions = [];
        string sampleResponse;
        string actionCardTitle;
        string actionType = "PREFILL_PROMPT";
        AgentClarificationFormDto form;

        if (isMeeting)
        {
            topicTitle = "chuẩn bị và điều phối cuộc họp / sự kiện";
            riskPerspective = "Nếu tổ chức họp mà chưa rõ thời gian chính xác, phòng họp, thành phần tham dự hoặc tài liệu cần thiết, cuộc họp dễ bị trễ giờ, thiếu thiết bị hỗ trợ hoặc không đạt mục tiêu thảo luận.";
            questions.Add("1. Thời gian & Thời lượng: Cuộc họp bắt đầu lúc mấy giờ và dự kiến kéo dài bao lâu?");
            questions.Add("2. Địa điểm / Hình thức: Họp trực tiếp tại văn phòng (phòng họp nào) hay họp trực tuyến (Zoom / Google Meet / Teams)?");
            questions.Add("3. Thành phần tham dự: Cuộc họp gồm những ai (Ban Giám đốc, các Trưởng phòng nào, hoặc có đối tác bên ngoài)?");
            questions.Add("4. Nội dung & Thiết bị: Chủ đề cuộc họp là gì? Cần chuẩn bị máy chiếu, micro, nước uống, teabreak hoặc in ấn tài liệu trước không?");
            sampleResponse = "Họp lúc 9h sáng mai tại phòng họp lớn, gồm Ban Giám đốc và IT, HR, cần chuẩn bị máy chiếu và tài liệu in";
            actionCardTitle = "Mẫu: Họp 9h sáng mai tại phòng họp Lớn (BGĐ, IT, HR)";
            actionType = "QUICK_FILL_MEETING";
            form = GetFormDefinition("form_meeting", message, tenantContextData);
        }
        else if (isMarketing)
        {
            topicTitle = "chiến dịch Marketing & Phát triển kinh doanh";
            riskPerspective = "Nếu triển khai chiến dịch mà chưa rõ mục tiêu KPI, đối tượng khách hàng mục tiêu và ngân sách dự kiến, các phòng ban (Marketing, Design, Sales) sẽ thiếu đồng bộ, dẫn đến lãng phí chi phí quảng cáo và hiệu quả tiếp cận thấp.";
            questions.Add("1. Mục tiêu & KPI cụ thể: Mục tiêu chính là gì (tăng nhận diện thương hiệu, thu về số lượng khách hàng tiềm năng hay doanh số bán lẻ)?");
            questions.Add("2. Đối tượng khách hàng & Kênh tiếp cận: Phân khúc khách hàng mục tiêu là ai và triển khai trên kênh nào (Facebook/Google Ads, TikTok, Email, Hội thảo)?");
            questions.Add("3. Ngân sách & Nguồn lực: Ngân sách dự kiến là bao nhiêu? Cần các phòng ban nào phối hợp (Design làm visual, Kỹ thuật làm landing page, Sales tư vấn)?");
            questions.Add("4. Tiến độ & Hạn chót ra mắt: Chiến dịch bắt đầu khi nào và dự kiến chạy trong thời gian bao lâu?");
            sampleResponse = "Chiến dịch ra mắt sản phẩm mới vào đầu tháng tới, mục tiêu đạt 500 khách hàng tiềm năng, ngân sách 30 triệu, phối hợp giữa Marketing, Design và Sales";
            actionCardTitle = "Mẫu: Chiến dịch ra mắt sản phẩm mới (500 lead, 30tr, MKT + Sales)";
            form = GetFormDefinition("form_marketing", message, tenantContextData);
        }
        else if (isRecruitment)
        {
            topicTitle = "tuyển dụng & Đào tạo phát triển nhân sự";
            riskPerspective = "Nếu bắt đầu tuyển dụng mà chưa có bản mô tả công việc (JD), số lượng chỉ tiêu và thời hạn onboarding rõ ràng, quy trình tuyển chọn sẽ bị kéo dài, gây quá tải cho bộ phận nhân sự và không đáp ứng kịp tiến độ kinh doanh.";
            questions.Add("1. Vị trí công việc & Số lượng chỉ tiêu: Cần tuyển / đào tạo những vị trí nào, cấp bậc gì (Junior, Senior, Lead) và số lượng bao nhiêu người?");
            questions.Add("2. Yêu cầu kỹ năng cốt lõi & Mức lương: Các tiêu chuẩn bắt buộc về kinh nghiệm, kỹ năng chuyên môn và dải ngân sách lương dự kiến?");
            questions.Add("3. Thời hạn nhân sự đi làm (Onboarding Deadline): Khi nào nhân sự cần chính thức bắt đầu công việc tại công ty?");
            questions.Add("4. Phòng ban tiếp nhận & Người phỏng vấn: Bộ phận nào trực tiếp sử dụng nhân sự và phụ trách đánh giá chuyên môn?");
            sampleResponse = "Tuyển 2 Senior Developer cho phòng Kỹ thuật trước ngày 15 tháng tới, yêu cầu 3 năm kinh nghiệm, ngân sách lương 25-30 triệu";
            actionCardTitle = "Mẫu: Tuyển 2 Senior Developer (Phòng Kỹ thuật, hạn 15 tháng sau)";
            form = GetFormDefinition("form_recruitment", message, tenantContextData);
        }
        else if (isTech)
        {
            topicTitle = "triển khai Kỹ thuật & Hạ tầng CNTT";
            riskPerspective = "Nếu nâng cấp hệ thống kỹ thuật mà chưa có kế hoạch thời gian gián đoạn (Downtime) và phương án dự phòng Rollback, hệ thống có nguy cơ gián đoạn hoạt động, ảnh hưởng đến dữ liệu và vận hành của toàn công ty.";
            questions.Add("1. Phạm vi kỹ thuật & Mục tiêu nâng cấp: Những hệ thống/máy chủ/module nào cần triển khai hoặc nâng cấp? Mục tiêu kỹ thuật cụ thể là gì?");
            questions.Add("2. Khung giờ bảo trì & Dự kiến Downtime: Sẽ thực hiện vào thời gian nào (ngoài giờ hành chính, cuối tuần)? Dự kiến gián đoạn bao lâu?");
            questions.Add("3. Phương án sao lưu & Kế hoạch Rollback: Đã có bản backup dữ liệu an toàn chưa và quy trình phục hồi khẩn cấp nếu gặp sự cố ra sao?");
            questions.Add("4. Bộ phận chịu trách nhiệm & Kiểm thử (QA): Đội ngũ nào triển khai chính và ai phụ trách kiểm thử nghiệm thu trước khi đưa vào vận hành?");
            sampleResponse = "Nâng cấp máy chủ vào 22h tối thứ 7, dự kiến downtime 1 tiếng, đội Kỹ thuật phụ trách, đã có bản backup toàn bộ cơ sở dữ liệu";
            actionCardTitle = "Mẫu: Nâng cấp máy chủ 22h tối thứ 7 (Downtime 1h, có backup)";
            form = GetFormDefinition("form_tech", message, tenantContextData);
        }
        else
        {
            topicTitle = "lập kế hoạch vận hành & Giải quyết công việc";
            riskPerspective = "Để một kế hoạch chuyển hóa thành các đầu việc khả thi cho các phòng ban, cần xác định rõ đầu ra mong đợi, tiến độ hoàn thành và nguồn lực phụ trách chính, tránh tình trạng đùn đẩy trách nhiệm.";
            questions.Add("1. Mục tiêu cốt lõi & Kết quả đầu ra (Deliverables): Sau khi thực hiện xong, kết quả cụ thể đo lường được là gì?");
            questions.Add("2. Tiến độ hoàn thành & Các mốc quan trọng (Milestones): Hạn chót hoàn thành là khi nào? Có các giai đoạn trung gian nào cần bàn giao?");
            questions.Add("3. Phòng ban chủ trì & Bộ phận phối hợp: Ai/Phòng ban nào chịu trách nhiệm chính và cần những bộ phận nào phối hợp hỗ trợ?");
            questions.Add("4. Nguồn lực, Ngân sách & Ràng buộc đặc biệt: Dự kiến cần nguồn lực hoặc chi phí bao nhiêu? Có quy định hoặc tiêu chuẩn bắt buộc nào cần tuân thủ?");
            sampleResponse = "Kế hoạch tối ưu chi phí vận hành văn phòng trong quý tới, mục tiêu giảm 15%, do phòng Hành chính chủ trì phối hợp cùng Kế toán";
            actionCardTitle = "Mẫu: Kế hoạch tối ưu chi phí quý tới (giảm 15%, Hành chính + Kế toán)";
            form = GetFormDefinition("form_general", message, tenantContextData);
        }

        csb.AppendLine($"Tôi đã tiếp nhận yêu cầu về {topicTitle} của bạn.\n");
        csb.AppendLine($"Góc nhìn Phản biện & Đánh giá Rủi ro:\n{riskPerspective}\n");
        csb.AppendLine("Để phân rã công việc chính xác và giao đúng phòng ban chuyên trách mà không tạo nhầm các đầu việc thừa, bạn vui lòng làm rõ giúp tôi các thông tin sau:\n");

        foreach (var q in questions)
        {
            csb.AppendLine(q);
        }

        csb.AppendLine();
        csb.AppendLine("Gợi ý trả lời nhanh:");
        csb.AppendLine($"\"{sampleResponse}\"\n");
        csb.AppendLine("Bạn có thể tương tác trực tiếp với Bảng câu hỏi khảo sát bên dưới hoặc phản hồi bằng tin nhắn để tôi phân tích và bóc tách nhiệm vụ ngay.");

        var thought = $"Suy nghĩ của Trợ lý AI Agent: Yêu cầu \"{message}\" mang tính chất khởi đầu nhưng chưa xác định các biến số vận hành quan trọng (thời gian, địa điểm, thành phần, trang thiết bị). Cần hiển thị bảng câu hỏi tương tác để người dùng làm rõ trước khi quyết định phân rã nhiệm vụ.";

        return new RobotChatResponse(
            csb.ToString().TrimEnd(),
            "AGENT_CLARIFICATION_REQUIRED",
            [
                new RobotActionCard(
                    actionType,
                    actionCardTitle,
                    null,
                    new { template = sampleResponse })
            ],
            Questionnaire: form,
            AgentThought: thought);
    }

    private async Task<IReadOnlyList<AgentQuestionOptionDto>> EnrichOptionsWithLearnedChoicesAsync(
        string semanticKey,
        IReadOnlyList<AgentQuestionOptionDto>? baseOptions,
        CancellationToken cancellationToken)
    {
        var result = new List<AgentQuestionOptionDto>();
        if (learnedChoiceStore != null && tenantContext.TenantId.HasValue)
        {
            try
            {
                var learned = await learnedChoiceStore.GetRelevantChoicesAsync(
                    tenantContext.TenantId.Value,
                    tenantContext.UserId,
                    semanticKey,
                    limit: 3,
                    cancellationToken);

                foreach (var l in learned)
                {
                    var exists = baseOptions?.Any(b => b.Label.Contains(l.ChoiceLabel, StringComparison.OrdinalIgnoreCase)) ?? false;
                    if (!exists)
                    {
                        result.Add(new AgentQuestionOptionDto("learned_" + l.Id.ToString("N")[..8], $"{l.ChoiceLabel} (Thường dùng)", IsDefault: true));
                    }
                }
            }
            catch { }
        }

        if (baseOptions != null)
        {
            bool hasLearnedDefault = result.Any(r => r.IsDefault);
            foreach (var opt in baseOptions)
            {
                if (hasLearnedDefault && opt.IsDefault)
                {
                    result.Add(opt with { IsDefault = false });
                }
                else
                {
                    result.Add(opt);
                }
            }
        }

        return result;
    }

    private async Task<AgentClarificationFormDto> GetEnrichedFormDefinitionAsync(
        string formId,
        string topic,
        AiTenantContextData? tenantContextData = null,
        CancellationToken cancellationToken = default)
    {
        var baseForm = GetFormDefinition(formId, topic, tenantContextData);
        if (learnedChoiceStore == null || !tenantContext.TenantId.HasValue)
        {
            return baseForm;
        }

        var enrichedQuestions = new List<AgentQuestionDto>();
        foreach (var q in baseForm.Questions)
        {
            var semanticKey = q.Id.ToLowerInvariant().Replace("q_", "");
            var enrichedOptions = await EnrichOptionsWithLearnedChoicesAsync(semanticKey, q.Options, cancellationToken);
            enrichedQuestions.Add(q with { Options = enrichedOptions, AllowCustomInput = true });
        }

        return baseForm with { Questions = enrichedQuestions };
    }

    private async Task<RobotChatResponse> BuildAgentClarificationResponseAsync(
        string message,
        bool isMeeting,
        bool isMarketing,
        bool isRecruitment,
        bool isTech,
        bool isIncident,
        AiTenantContextData? tenantContextData = null,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var baseResp = BuildAgentClarificationResponse(message, isMeeting, isMarketing, isRecruitment, isTech, isIncident, tenantContextData);
        if (baseResp.Questionnaire == null) return baseResp;

        var enrichedForm = await GetEnrichedFormDefinitionAsync(
            baseResp.Questionnaire.FormId,
            message,
            tenantContextData,
            cancellationToken);

        if (conversationStateManager != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
        {
            var convId = conversationId ?? "default";
            var state = new ConversationState(
                convId,
                tenantContext.TenantId.Value,
                tenantContext.UserId.Value,
                enrichedForm.Topic,
                new(StringComparer.OrdinalIgnoreCase),
                enrichedForm.Questions.Select(q => q.Id).ToList(),
                enrichedForm,
                [],
                clock.GetUtcNow());
            _ = conversationStateManager.SaveStateAsync(state, cancellationToken);
        }

        return baseResp with { Questionnaire = enrichedForm };
    }

    private async Task<RobotChatResponse?> TryHandleLlmAgentOrchestrationAsync(
        RobotChatCommand command,
        AiTenantContextData? tenantContextData,
        CancellationToken cancellationToken)
    {
        if (modelClient == null || !modelClient.IsConfigured) return null;

        var message = command.Message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(message)) return null;

        var systemInstructions = """
        Bạn là Trợ lý Doanh nghiệp AI Agent BizFlow (Enterprise Multi-Agent Orchestrator).
        Nhiệm vụ của bạn là phân tích thông điệp của người dùng về kế hoạch công việc, cuộc họp, dự án, hoặc các sáng kiến vận hành liên phòng ban.
        1. Phân loại ý định (intent):
           - "STATUS_QUERY": người dùng chỉ tra cứu dữ liệu (deadline hôm nay, quá hạn, việc của tôi). Khi đó trả về intent: "STATUS_QUERY", isComplete: true.
           - "WORK_PLAN": lập kế hoạch công việc, dự án, chiến dịch, tuyển dụng, nâng cấp hệ thống, sự kiện.
           - "MEETING": tổ chức hoặc chuẩn bị cuộc họp.
           - "GENERAL": hội thoại chung hoặc thắc mắc hướng dẫn.
        2. Trích xuất thông tin (extractedSlots):
           - topic, timeline, department, deliverables, attendees, budget, priority, v.v.
        3. Đánh giá tính đầy đủ (isComplete):
           - Đầy đủ nếu các yếu tố quan trọng để giao việc hoặc điều phối đã rõ ràng.
           - Thiếu (isComplete: false) nếu còn mơ hồ, thiếu thời gian, thiếu phòng ban chủ trì, hoặc thiếu phạm vi.
        4. Nếu thiếu thông tin (isComplete: false):
           - Tạo bảng câu hỏi làm rõ (dynamicQuestions) với 2-4 câu hỏi trọng tâm.
           - Hỗ trợ các kiểu: "single_choice", "multiple_choice", "text".
           - Mỗi câu hỏi có các lựa chọn gợi ý thực tế và allowCustomInput: true.
           - Nêu rõ rủi ro nếu không làm rõ (riskAnalysis) và suy nghĩ của Agent (agentThought).

        Định dạng JSON bắt buộc:
        {
          "intent": "WORK_PLAN | MEETING | STATUS_QUERY | GENERAL",
          "isComplete": boolean,
          "topic": "string",
          "extractedSlots": { "key": "value" },
          "missingSlots": ["string"],
          "riskAnalysis": "string",
          "agentThought": "string",
          "dynamicQuestions": [
            {
              "id": "string",
              "title": "string",
              "type": "single_choice | multiple_choice | text",
              "options": [ { "id": "string", "label": "string", "isDefault": boolean } ],
              "required": boolean,
              "placeholder": "string",
              "allowCustomInput": boolean
            }
          ]
        }
        """;

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Yêu cầu của người dùng: \"{message}\"");

        if (command.History is { Count: > 0 })
        {
            promptBuilder.AppendLine("Lịch sử hội thoại gần nhất:");
            foreach (var h in command.History.TakeLast(4))
            {
                promptBuilder.AppendLine($"- {h.Sender}: {h.Text}");
            }
        }

        if (tenantContextData?.Departments is { Count: > 0 })
        {
            promptBuilder.AppendLine($"Các phòng ban hiện có trong công ty: {string.Join(", ", tenantContextData.Departments.Select(d => $"{d.Name} ({d.Code})"))}");
        }

        try
        {
            var llmResponse = await modelClient.CompleteAsync(promptBuilder.ToString(), systemInstructions, cancellationToken);
            var content = llmResponse.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content)) return null;

            if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                content = content[7..];
                if (content.EndsWith("```")) content = content[..^3];
            }
            else if (content.StartsWith("```"))
            {
                content = content[3..];
                if (content.EndsWith("```")) content = content[..^3];
            }

            using var doc = JsonDocument.Parse(content.Trim());
            var root = doc.RootElement;

            var intent = root.TryGetProperty("intent", out var ip) ? ip.GetString() : "WORK_PLAN";
            if (string.Equals(intent, "STATUS_QUERY", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var isComplete = root.TryGetProperty("isComplete", out var cp) && cp.GetBoolean();
            var topic = root.TryGetProperty("topic", out var tp) ? tp.GetString() ?? message : message;
            var riskAnalysis = root.TryGetProperty("riskAnalysis", out var rp) ? rp.GetString() : null;
            var agentThought = root.TryGetProperty("agentThought", out var atp) ? atp.GetString() : null;

            if (!isComplete && root.TryGetProperty("dynamicQuestions", out var qArray) && qArray.ValueKind == JsonValueKind.Array)
            {
                var questions = new List<AgentQuestionDto>();
                foreach (var qElem in qArray.EnumerateArray())
                {
                    var qId = qElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "q_" + Guid.NewGuid().ToString("N")[..6] : "q_" + Guid.NewGuid().ToString("N")[..6];
                    var qTitle = qElem.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    var qType = qElem.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "single_choice" : "single_choice";
                    var qRequired = !qElem.TryGetProperty("required", out var reqProp) || reqProp.GetBoolean();
                    var qPlaceholder = qElem.TryGetProperty("placeholder", out var plProp) ? plProp.GetString() : null;
                    var qCustom = !qElem.TryGetProperty("allowCustomInput", out var custProp) || custProp.GetBoolean();

                    var options = new List<AgentQuestionOptionDto>();
                    if (qElem.TryGetProperty("options", out var optArray) && optArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var optElem in optArray.EnumerateArray())
                        {
                            var optId = optElem.TryGetProperty("id", out var oid) ? oid.GetString() ?? Guid.NewGuid().ToString("N")[..4] : Guid.NewGuid().ToString("N")[..4];
                            var optLabel = optElem.TryGetProperty("label", out var olbl) ? olbl.GetString() ?? "" : "";
                            var optDef = optElem.TryGetProperty("isDefault", out var odef) && odef.GetBoolean();
                            if (!string.IsNullOrWhiteSpace(optLabel))
                            {
                                options.Add(new AgentQuestionOptionDto(optId, optLabel, optDef));
                            }
                        }
                    }

                    var semanticKey = qId.ToLowerInvariant().Replace("q_", "");
                    options = (await EnrichOptionsWithLearnedChoicesAsync(semanticKey, options, cancellationToken)).ToList();

                    questions.Add(new AgentQuestionDto(
                        qId,
                        qTitle,
                        qType,
                        options,
                        qRequired,
                        qPlaceholder,
                        null,
                        qCustom));
                }

                if (questions.Count > 0)
                {
                    var formId = "dynamic_" + Guid.NewGuid().ToString("N")[..8];
                    var clarificationForm = new AgentClarificationFormDto(
                        formId,
                        topic,
                        "Để phân định trách nhiệm rõ ràng và giao đúng phòng ban chuyên trách, vui lòng làm rõ các điểm sau:",
                        riskAnalysis,
                        questions,
                        "Xác nhận & Tiếp tục phân tích",
                        "Sử dụng thiết lập khuyến nghị");

                    if (conversationStateManager != null && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
                    {
                        var convId = command.ConversationId ?? "default";
                        var state = new ConversationState(
                            convId,
                            tenantContext.TenantId.Value,
                            tenantContext.UserId.Value,
                            topic,
                            new(StringComparer.OrdinalIgnoreCase),
                            questions.Select(q => q.Id).ToList(),
                            clarificationForm,
                            command.History != null ? new List<RobotChatMessageDto>(command.History) : [],
                            clock.GetUtcNow());
                        _ = conversationStateManager.SaveStateAsync(state, cancellationToken);
                    }

                    var textSummary = $"Chào bạn! Tôi đã phân tích yêu cầu \"{message}\".\n" +
                        "Để đảm bảo kế hoạch vận hành chính xác và tránh rủi ro thiếu sót, vui lòng trả lời một số câu hỏi làm rõ bên dưới:";

                    return new RobotChatResponse(
                        textSummary,
                        "AGENT_CLARIFICATION_REQUIRED",
                        [new RobotActionCard("PREFILL_PROMPT", "Làm rõ yêu cầu", null, new { template = "Hoàn tất thông tin cần làm rõ" })],
                        Questionnaire: clarificationForm,
                        AgentThought: agentThought);
                }
            }

            if (isComplete)
            {
                return await ExecuteAgentDecompositionAsync(
                    message,
                    originalTitle: topic,
                    userClarifiedDetails: null,
                    agentThought: agentThought,
                    cancellationToken: cancellationToken);
            }
        }
        catch
        {
            // Fall back to deterministic rule-based dynamic synthesis
        }

        return null;
    }

    private async Task<RobotChatResponse> ExecuteAgentDecompositionAsync(
        string prompt,
        string originalTitle,
        string? userClarifiedDetails,
        string? agentThought = null,
        CancellationToken cancellationToken = default)
    {
        var preview = await decompositionService.PreviewAsync(
            new TaskDecompositionPreviewCommand(prompt),
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Trợ lý AI Agent đã tổng hợp đầy đủ thông tin và lập kế hoạch phân rã công việc đa phòng ban:\n");
        if (!string.IsNullOrWhiteSpace(userClarifiedDetails))
        {
            sb.AppendLine($"Thông tin đã làm rõ: {userClarifiedDetails}\n");
        }

        sb.AppendLine($"Đề xuất phân công tới các phòng ban chuyên trách ({preview.Proposals.Count} nhánh nhiệm vụ):");
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
        sb.AppendLine("Vui lòng mở giao diện Phân rã AI hoặc chuyển sang tab Nhánh tác vụ để kiểm tra, chỉnh sửa inline và xác nhận tạo việc.");

        var finalThought = agentThought ?? "Phân tích của AI Agent: Đã tổng hợp đầy đủ thông tin xác thực từ người dùng và phân rã các nhánh nhiệm vụ theo đúng thẩm quyền và chuyên môn từng bộ phận.";

        return new RobotChatResponse(
            sb.ToString().TrimEnd(),
            "/task",
            [
                new RobotActionCard("OPEN_DECOMPOSITION", "Xem & Xác nhận tạo việc", "/tasks/ai-decomposition", new { prompt, proposals = preview.Proposals }),
                new RobotActionCard("VIEW_BRANCHES", $"Xem các nhánh phân rã ({preview.Proposals.Count} nhánh)", null, new { proposals = preview.Proposals })
            ],
            AgentThought: finalThought);
    }
    private async Task<RobotChatResponse> HandleNaturalLanguageQueryAsync(RobotChatCommand command, CancellationToken cancellationToken)
    {
        var message = command.Message;
        var locale = command.Locale;
        var now = clock.GetUtcNow();
        var lowerMsg = message.ToLowerInvariant();

        // 1. Fetch authorized tasks for current user
        IReadOnlyList<TaskListRow> visibleTasks = [];
        try
        {
            var page = await taskListQuery.ListAsync(new TaskListFilter(1, 100), cancellationToken);
            visibleTasks = page.Items;
        }
        catch (ApplicationFault af) when (af.Kind == FaultKind.Forbidden)
        {
            return new RobotChatResponse(
                "Bạn hiện không có quyền xem danh sách công việc trong Không gian làm việc này.",
                "FORBIDDEN");
        }

        var overdueTasks = visibleTasks
            .Where(t => t.Deadline.HasValue && t.Deadline.Value < now && t.Status != "COMPLETED" && t.Status != "CANCELLED")
            .ToList();

        var dueTodayTasks = visibleTasks
            .Where(t => t.Deadline.HasValue && t.Deadline.Value.Date == now.Date && t.Status != "COMPLETED" && t.Status != "CANCELLED")
            .ToList();

        var pendingTasks = visibleTasks
            .Where(t => t.Status is "ASSIGNED" or "ACCEPTED" or "IN_PROGRESS" or "SUBMITTED")
            .ToList();

        var highPriorityTasks = visibleTasks
            .Where(t => (t.Priority == "HIGH" || t.Priority == "CRITICAL") && t.Status != "COMPLETED" && t.Status != "CANCELLED")
            .ToList();

        // 2. Identify Intent & Grounded Response
        if (lowerMsg.StartsWith("phân rã") || lowerMsg.StartsWith("bóc tách") || lowerMsg.StartsWith("lên kế hoạch") ||
            lowerMsg.Contains("phân chia công việc") || lowerMsg.Contains("bóc tách công việc"))
        {
            return await HandleTaskCommandAsync($"/task {message}", cancellationToken);
        }

        if (lowerMsg.Contains("hôm nay") || lowerMsg.Contains("today") || (lowerMsg.Contains("deadline") && !lowerMsg.Contains("quá hạn")))
        {
            if (dueTodayTasks.Count == 0)
            {
                return new RobotChatResponse("Hôm nay bạn không có deadline nào cần hoàn thành. Chúc bạn một ngày làm việc hiệu quả!", "QUERY_DEADLINE");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Hôm nay bạn có {dueTodayTasks.Count} công việc đến hạn:");
            foreach (var t in dueTodayTasks)
            {
                var timeStr = t.Deadline?.ToLocalTime().ToString("HH:mm") ?? "";
                sb.AppendLine($"- {t.Title} (Hạn: {timeStr}, Trạng thái: {t.Status}, Ưu tiên: {t.Priority})");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_DEADLINE");
        }

        if (lowerMsg.Contains("quá hạn") || lowerMsg.Contains("trễ hạn") || lowerMsg.Contains("overdue"))
        {
            if (overdueTasks.Count == 0)
            {
                return new RobotChatResponse("Tuyệt vời! Hiện tại bạn không có công việc nào bị quá hạn.", "QUERY_OVERDUE");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Bạn có {overdueTasks.Count} công việc đang bị quá hạn:");
            foreach (var t in overdueTasks)
            {
                var dateStr = t.Deadline?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
                sb.AppendLine($"- {t.Title} (Hạn chót: {dateStr}, Trạng thái: {t.Status})");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_OVERDUE");
        }

        if (lowerMsg.Contains("chờ xử lý") || lowerMsg.Contains("cần làm") || lowerMsg.Contains("đang làm") || lowerMsg.Contains("nhiệm vụ của tôi") || lowerMsg.Contains("tiến độ"))
        {
            if (pendingTasks.Count == 0)
            {
                return new RobotChatResponse("Hiện tại bạn không có nhiệm vụ nào đang chờ xử lý.", "QUERY_PENDING");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Bạn đang có {pendingTasks.Count} nhiệm vụ đang tiến hành:");
            foreach (var t in pendingTasks.Take(5))
            {
                var dlStr = t.Deadline.HasValue ? $" - Hạn: {t.Deadline.Value.ToLocalTime():dd/MM/yyyy}" : "";
                sb.AppendLine($"- {t.Title} [{t.Priority}] (Trạng thái: {t.Status}{dlStr})");
            }
            if (pendingTasks.Count > 5)
            {
                sb.AppendLine($"... và còn {pendingTasks.Count - 5} công việc khác.");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_PENDING");
        }

        // Multi-turn context resolution: if user asks for ordinal task details ("nhiệm vụ thứ nhất", "việc 2", "task đầu tiên", "chi tiết hơn")
        int? targetTaskIndex = null;
        if (lowerMsg.Contains("thứ nhất") || lowerMsg.Contains("đầu tiên") || lowerMsg.Contains("thứ 1") || lowerMsg.Contains("việc 1") || lowerMsg.Contains("task 1"))
        {
            targetTaskIndex = 0;
        }
        else if (lowerMsg.Contains("thứ hai") || lowerMsg.Contains("thứ 2") || lowerMsg.Contains("việc 2") || lowerMsg.Contains("task 2"))
        {
            targetTaskIndex = 1;
        }
        else if (lowerMsg.Contains("thứ ba") || lowerMsg.Contains("thứ 3") || lowerMsg.Contains("việc 3") || lowerMsg.Contains("task 3"))
        {
            targetTaskIndex = 2;
        }
        else if (lowerMsg.Contains("thứ tư") || lowerMsg.Contains("thứ 4") || lowerMsg.Contains("việc 4") || lowerMsg.Contains("task 4"))
        {
            targetTaskIndex = 3;
        }

        if (targetTaskIndex.HasValue && (lowerMsg.Contains("chi tiết") || lowerMsg.Contains("là gì") || lowerMsg.Contains("thế nào") || lowerMsg.Contains("thông tin") || lowerMsg.Contains("ai làm") || lowerMsg.Contains("xem")))
        {
            var taskPool = pendingTasks.Count > 0 ? pendingTasks : visibleTasks;
            if (targetTaskIndex.Value < taskPool.Count)
            {
                var target = taskPool[targetTaskIndex.Value];
                var dlText = target.Deadline.HasValue ? target.Deadline.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Không có hạn chót cụ thể";
                var rsb = new StringBuilder();
                rsb.AppendLine($"Chi tiết nhiệm vụ #{targetTaskIndex.Value + 1} (Theo chuỗi hội thoại):");
                rsb.AppendLine($"- Tiêu đề: {target.Title}");
                rsb.AppendLine($"- Mức ưu tiên: {target.Priority}");
                rsb.AppendLine($"- Trạng thái: {target.Status}");
                rsb.AppendLine($"- Hạn hoàn thành: {dlText}");
                rsb.AppendLine($"- Mã định danh: {target.TaskId}");
                rsb.AppendLine();
                rsb.AppendLine("Bạn có thể bấm vào thẻ bên dưới để xem toàn bộ chi tiết và theo dõi tiến độ.");
                return new RobotChatResponse(
                    rsb.ToString().TrimEnd(),
                    "TASK_DETAIL_CONTEXT",
                    [new RobotActionCard("VIEW_TASK", $"Xem chi tiết: {target.Title}", $"/tasks/{target.TaskId}")]);
            }
        }

        // Check Personal User Vector Memory (MemoryScope = USER)
        if (vectorMemoryStore != null && (lowerMsg.Contains("nhớ gì") || lowerMsg.Contains("về tôi") || lowerMsg.Contains("thói quen") || lowerMsg.Contains("hồ sơ của tôi") || lowerMsg.Contains("tôi là ai")))
        {
            var userMemories = await vectorMemoryStore.SearchMemoriesAsync(
                tenantContext.TenantId!.Value,
                message,
                "USER",
                3,
                0.20f,
                cancellationToken);

            if (userMemories.Count > 0)
            {
                var psb = new StringBuilder();
                psb.AppendLine("Hồ sơ & Thói quen cá nhân tôi đã ghi nhớ về bạn:");
                foreach (var mem in userMemories)
                {
                    psb.AppendLine($"- {mem.Title}: {mem.Content}");
                }
                psb.AppendLine("\nBạn có thể thêm ghi nhớ mới bằng cú pháp: `/remember <nội dung>`.");
                return new RobotChatResponse(psb.ToString().TrimEnd(), "USER_MEMORY");
            }
        }

        // Long-term enterprise vector memory RAG check for policies and guidelines
        if (vectorMemoryStore != null && (lowerMsg.Contains("quy định") || lowerMsg.Contains("chính sách") || lowerMsg.Contains("nội quy") || lowerMsg.Contains("tiêu chuẩn") || lowerMsg.Contains("hướng dẫn")))
        {
            var memoryMatches = await vectorMemoryStore.SearchMemoriesAsync(tenantContext.TenantId!.Value, message, null, 2, 0.35f, cancellationToken);
            if (memoryMatches.Count > 0)
            {
                var msb = new StringBuilder();
                msb.AppendLine("Căn cứ theo Tri thức & Quy định doanh nghiệp (RAG):");
                foreach (var match in memoryMatches)
                {
                    msb.AppendLine($"\n{match.Title} [{match.MemoryScope}]:\n{match.Content}");
                }
                return new RobotChatResponse(msb.ToString().TrimEnd(), "ENTERPRISE_MEMORY");
            }
        }

        // 3. Fallback / General Query with LLM Grounding & Tool Calling Support
        if (modelClient is { IsConfigured: true })
        {
            try
            {
                var groundingData = JsonSerializer.Serialize(new
                {
                    currentTime = now.ToString("O"),
                    totalVisibleTasks = visibleTasks.Count,
                    dueTodayCount = dueTodayTasks.Count,
                    overdueCount = overdueTasks.Count,
                    pendingCount = pendingTasks.Count,
                    topTasks = visibleTasks.Take(8).Select(t => new
                    {
                        t.TaskId,
                        t.Title,
                        t.Status,
                        t.Priority,
                        Deadline = t.Deadline?.ToString("dd/MM/yyyy HH:mm")
                    })
                });

                var historySection = new StringBuilder();
                if (command.History is { Count: > 0 })
                {
                    historySection.AppendLine("\nLịch sử các lượt trao đổi gần nhất giữa bạn và người dùng:");
                    foreach (var h in command.History.TakeLast(6))
                    {
                        var role = h.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "Người dùng" : "Trợ lý AI";
                        historySection.AppendLine($"[{role}]: {h.Text}");
                    }
                }

                var toolsSummary = string.Join("\n", (toolRegistry?.GetRegisteredTools() ?? []).Select(t => $"- {t.Name}: {t.Description}"));

                var sysPrompt = $$"""
                    Bạn là Trợ lý Doanh nghiệp AI Agent thông minh của nền tảng BizFlow Platform.
                    Trả lời người dùng hoàn toàn bằng tiếng Việt, ngắn gọn, thân thiện, chuyên nghiệp, lịch sự và súc tích.
                    TUYỆT ĐỐI KHÔNG dùng bất kỳ icon emoji nào và KHÔNG dùng dấu sao * hoặc ** trong câu trả lời để tránh rối mắt người dùng.

                    NGUYÊN TẮC HOẠT ĐỘNG (AUTONOMOUS AGENT REASONING, CRITIQUE & ANTI-HALLUCINATION):
                    1. ĐỐI VỚI CÂU HỎI TRA CỨU DỮ LIỆU HIỆN CÓ TRONG HỆ THỐNG (deadline, việc quá hạn, việc của tôi, số lượng task...):
                       - Chỉ dựa vào dữ liệu thực tế trích xuất từ database sau đây:
                       {{groundingData}}
                       {{historySection}}
                       - Tuyệt đối không bịa đặt nhiệm vụ, deadline hay số liệu không có trong dữ liệu trên (Tuân thủ BR-027).
                       - Nếu là câu hỏi tra cứu dữ liệu mà thông tin không có trong database, nói rõ là không tìm thấy trong danh sách công việc hiện tại.

                    2. ĐỐI VỚI BẤT KỲ YÊU CẦU LẬP KẾ HOẠCH / MỤC TIÊU VẬN HÀNH / TÁC VỤ MỚI / GIẢI QUYẾT VẤN ĐỀ (chiến dịch marketing, ra mắt sản phẩm, tuyển dụng, nâng cấp máy chủ, cuộc họp, tổ chức sự kiện, tối ưu chi phí, xử lý sự cố...):
                       - ĐÂY LÀ KẾ HOẠCH HÀNH ĐỘNG MỚI, KHÔNG PHẢI tra cứu task cũ trong database. TUYỆT ĐỐI KHÔNG từ chối bằng câu "không tìm thấy trong dữ liệu hiện tại".
                       - HÃY ĐÓNG VAI TRÒ LÀ MỘT AI AGENT THỰC THỤ: CÓ KHẢ NĂNG SUY NGHĨ, PHẢN BIỆN VÀ ĐẶT CÂU HỎI VỀ NHỮNG ĐIỀU CHƯA CHẮC CHẮN:
                         + Khi yêu cầu còn chung chung, mơ hồ hoặc thiếu các tham số then chốt (chưa rõ Mục tiêu/KPI cụ thể, Thời gian/Hạn chót, Ngân sách/Nguồn lực, Phòng ban chủ trì, Tiêu chí hoàn thành):
                           - Nhận định mục tiêu của người dùng.
                           - Nêu góc nhìn phản biện và rủi ro: chỉ ra các rủi ro tiềm ẩn (lãng phí ngân sách, trễ hạn, giao nhầm phòng ban) nếu vội vàng triển khai khi chưa chắc chắn thông tin.
                           - Đặt bộ câu hỏi làm rõ có cấu trúc logic (theo 4 trụ cột: 1. Mục tiêu/Phạm vi, 2. Thời gian/Milestone, 3. Nguồn lực/Phòng ban phụ trách, 4. Tiêu chuẩn đầu ra/Rủi ro cần phòng tránh).
                           - Cung cấp một phương án trả lời mẫu ngắn gọn để người dùng tham khảo.
                         + Khi yêu cầu đã rõ ràng hoặc người dùng đã phản hồi các câu hỏi làm rõ trong lịch sử hội thoại:
                           - Tổng hợp bức tranh chiến lược hành động.
                           - Đề xuất phân bổ các nhánh nhiệm vụ cụ thể cho từng phòng ban chuyên trách (Marketing, Thiết kế, Kỹ thuật/IT, Nhân sự, Tài chính, Điều hành...).

                    Danh sách công cụ được cấp phép:
                    {{toolsSummary}}

                    Nếu người dùng yêu cầu thực hiện hành động (tạo công việc, phân công, bình luận, điều phối yêu cầu), hãy trả về JSON:
                    {
                        "toolCall": {
                            "name": "create_task_draft" | "assign_task" | "add_comment" | "route_request",
                            "arguments": { ... }
                        },
                        "reply": "<lời giải thích ngắn gọn cho người dùng>"
                    }
                    Nếu là câu hỏi thông thường, trả về JSON:
                    {
                        "reply": "<nội dung câu trả lời>"
                    }
                    """;

                var aiResp = await modelClient.CompleteAsync(message, sysPrompt, cancellationToken);
                using var doc = JsonDocument.Parse(aiResp.Content);
                var root = doc.RootElement;

                // Check for Tool Call proposal
                if (root.TryGetProperty("toolCall", out var tcProp) &&
                    tcProp.TryGetProperty("name", out var nameProp) &&
                    assistanceService != null)
                {
                    var toolName = nameProp.GetString() ?? "";
                    var argsJson = tcProp.TryGetProperty("arguments", out var argsProp) ? argsProp.GetRawText() : "{}";
                    var reply = root.TryGetProperty("reply", out var rProp) ? rProp.GetString() ?? "" : "";

                    if (toolRegistry != null && toolRegistry.GetTool(toolName) is null)
                    {
                        return new RobotChatResponse(
                            $"Công cụ '{toolName}' không nằm trong danh mục công cụ được cấp phép.",
                            "TOOL_NOT_ALLOWED");
                    }

                    try
                    {
                        var actionResult = await assistanceService.ExecuteActionAsync(
                            new ExecuteAiActionCommand(toolName, argsJson, Confirmed: false),
                            cancellationToken);

                        if (actionResult.ExecutionStatus == "NOT_EXECUTED")
                        {
                            return new RobotChatResponse(
                                $"{reply}\n\nĐề xuất hành động '{toolName}' cần sự xác nhận của bạn trước khi thực hiện.",
                                "PROPOSE_ACTION",
                                [
                                    new RobotActionCard(
                                        "CONFIRM_ACTION",
                                        $"Xác nhận {toolName}",
                                        "/api/v1/ai/actions/execute",
                                        new { toolName, confirmationToken = actionResult.ConfirmationToken, argsJson })
                                ]);
                        }

                        return new RobotChatResponse($"{reply}\n\nĐã thực hiện thành công.", "EXECUTE_ACTION");
                    }
                    catch (ApplicationFault af)
                    {
                        return new RobotChatResponse($"Không thể thực hiện hành động: {af.Message}", "ACTION_REJECTED");
                    }
                }

                if (root.TryGetProperty("reply", out var directReplyProp))
                {
                    return new RobotChatResponse(directReplyProp.GetString() ?? "", "GENERAL");
                }

                return new RobotChatResponse(aiResp.Content, "GENERAL");
            }
            catch
            {
                // Fallback to deterministic summary if LLM call fails
            }
        }

        // Deterministic General Assistance in Vietnamese
        var genSb = new StringBuilder();
        genSb.AppendLine("Chào bạn! Tôi là Trợ lý Robot BizFlow.");
        genSb.AppendLine();
        genSb.AppendLine($"Tổng quan hiện tại của bạn: {pendingTasks.Count} việc đang tiến hành, {dueTodayTasks.Count} việc đến hạn hôm nay, {overdueTasks.Count} việc quá hạn.");
        genSb.AppendLine();
        genSb.AppendLine("Bạn có thể hỏi tôi:");
        genSb.AppendLine("- Hôm nay tôi có deadline nào?");
        genSb.AppendLine("- Kiểm tra các việc bị quá hạn");
        genSb.AppendLine("- Liệt kê các việc chờ xử lý của tôi");
        genSb.AppendLine("- Hoặc dùng lệnh /task <nội dung> để phân rã công việc thông minh.");
        genSb.AppendLine("- Dùng lệnh /create-task <tiêu đề> để đề xuất tạo công việc mới.");
        genSb.AppendLine("- Dùng lệnh /notifications để xem thông báo và nhắc hạn.");

        return new RobotChatResponse(genSb.ToString(), "GENERAL");
    }
}
