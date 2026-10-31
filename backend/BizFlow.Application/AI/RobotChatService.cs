using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    IProactiveAgentScanner? proactiveScanner = null) : IRobotChatService
{
    public async Task<RobotChatResponse> ProcessMessageAsync(
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

        // 6. Natural Language Task and Deadline Query with Tool Orchestration, RBAC & Anti-Hallucination
        return await HandleNaturalLanguageQueryAsync(message, command.Locale, cancellationToken);
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
        sb.AppendLine($"🤖 Trợ lý Robot đã phân rã yêu cầu thành {preview.Proposals.Count} đề xuất công việc (Bản xem trước - Chưa lưu vào DB theo BR-028):");
        sb.AppendLine();

        for (var i = 0; i < preview.Proposals.Count; i++)
        {
            var p = preview.Proposals[i];
            var typeLabel = p.Type == "SupportRequest" ? "Yêu cầu hỗ trợ" : "Giao việc";
            sb.AppendLine($"{i + 1}. **{p.Title}** [{typeLabel}] - Mức ưu tiên: {p.Priority}");
            if (!string.IsNullOrWhiteSpace(p.DepartmentName))
            {
                sb.AppendLine($"   • Phòng ban: {p.DepartmentName}");
            }
            if (p.ChecklistItems is { Count: > 0 })
            {
                sb.AppendLine($"   • Các bước: {string.Join(", ", p.ChecklistItems)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("👉 Vui lòng mở giao diện Phân rã AI để kiểm tra, chỉnh sửa inline và xác nhận tạo việc.");

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
        sb.AppendLine("🔔 **Quản lý Thông báo và Nhắc hẹn (Deadline Reminders)**");
        sb.AppendLine();
        sb.AppendLine($"• Hiện tại bạn có **{unreadCount}** thông báo chưa đọc trong hòm thư.");
        sb.AppendLine("• Hệ thống hỗ trợ nhắc hạn đa kênh tự động tại 4 mốc: **24 giờ**, **4 giờ**, **1 giờ trước hạn** và **khi quá hạn**.");
        sb.AppendLine("• Kênh gửi thông báo: **In-App Toast (SignalR)** khi online, và **Web Push (VAPID)** khi không mở website.");
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
            sb.AppendLine("🤖 **Đề xuất tạo công việc mới:**");
            sb.AppendLine($"• Tiêu đề: **{title}**");
            sb.AppendLine();
            sb.AppendLine("⚠️ Hành động này cần sự xác nhận của bạn trước khi lưu vào hệ thống.");

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
            return new RobotChatResponse($"❌ Không thể đề xuất tạo việc: {af.Message}", "ACTION_REJECTED");
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
                    "🧠 **Bộ nhớ dài hạn & Tri thức doanh nghiệp (Vector Memory):**\n" +
                    "Chưa có tài liệu chính sách hoặc tiền lệ công việc nào được lập chỉ mục.\n" +
                    "Bạn có thể tra cứu theo cú pháp: `/memory <nội dung cần tìm>`.",
                    "/memory");
            }

            var sb = new StringBuilder();
            sb.AppendLine("🧠 **Bộ nhớ dài hạn & Quy định nội bộ gần nhất:**");
            foreach (var m in recent)
            {
                sb.AppendLine($"• [{m.MemoryScope}] **{m.Title}**: {Truncate(m.Content, 100)}");
            }
            sb.AppendLine("\nGõ `/memory <từ khóa>` để tìm kiếm tương đồng ngữ nghĩa (Cosine RAG).");
            return new RobotChatResponse(sb.ToString().TrimEnd(), "/memory");
        }

        var results = await vectorMemoryStore!.SearchMemoriesAsync(tenantContext.TenantId!.Value, query, null, 3, 0.30f, cancellationToken);
        if (results.Count == 0)
        {
            return new RobotChatResponse(
                $"🔍 Không tìm thấy chính sách hoặc tiền lệ nào khớp với nội dung '{query}' trong bộ nhớ doanh nghiệp.",
                "/memory");
        }

        var rsb = new StringBuilder();
        rsb.AppendLine($"🔍 **Kết quả tra cứu Bộ nhớ Vector (Khớp ngữ nghĩa cao nhất):**");
        foreach (var r in results)
        {
            var matchPercent = Math.Round(r.SimilarityScore * 100, 1);
            rsb.AppendLine($"\n📌 **{r.Title}** (Phạm vi: {r.MemoryScope} | Độ khớp: {matchPercent}%)");
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
                "⚡ **Agent Giám sát Chủ động (Autonomous Daemon):**\n" +
                "Hiện tại không có rủi ro trễ hạn SLA hoặc tình trạng quá tải nhân sự nào được phát hiện trong hệ thống. Mọi thứ đang vận hành ổn định!",
                "/proactive");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"⚡ **Phát hiện {insights.Count} khuyến nghị từ Agent Giám sát Chủ động:**\n");
        var cards = new List<RobotActionCard>();

        foreach (var insight in insights)
        {
            sb.AppendLine($"• **{insight.Title}** (Độ tin cậy: {Math.Round(insight.Confidence * 100)}%)");
            sb.AppendLine($"  {insight.Description}");
            if (insight.ObjectId.HasValue)
            {
                cards.Add(new RobotActionCard("VIEW_TASK", "Xem chi tiết", $"/tasks/{insight.ObjectId.Value}"));
            }
        }

        return new RobotChatResponse(sb.ToString().TrimEnd(), "/proactive", cards.Take(3).ToList());
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";

    private async Task<RobotChatResponse> HandleNaturalLanguageQueryAsync(string message, string? locale, CancellationToken cancellationToken)
    {
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
                var emptyMsg = locale == "ja" ? "📅 本日期限のタスクはありません。" : (locale == "en" ? "📅 You have no deadlines due today." : "📅 Hôm nay bạn không có deadline nào cần hoàn thành. Chúc bạn một ngày làm việc hiệu quả!");
                return new RobotChatResponse(emptyMsg, "QUERY_DEADLINE");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📅 Hôm nay bạn có **{dueTodayTasks.Count}** công việc đến hạn:");
            foreach (var t in dueTodayTasks)
            {
                var timeStr = t.Deadline?.ToLocalTime().ToString("HH:mm") ?? "";
                sb.AppendLine($"• **{t.Title}** (Hạn: {timeStr}, Trạng thái: {t.Status}, Ưu tiên: {t.Priority})");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_DEADLINE");
        }

        if (lowerMsg.Contains("quá hạn") || lowerMsg.Contains("trễ hạn") || lowerMsg.Contains("overdue"))
        {
            if (overdueTasks.Count == 0)
            {
                var noOverdueMsg = locale == "ja" ? "🎉 期限超過のタスクはありません。" : (locale == "en" ? "🎉 You have no overdue tasks." : "🎉 Tuyệt vời! Hiện tại bạn không có công việc nào bị quá hạn.");
                return new RobotChatResponse(noOverdueMsg, "QUERY_OVERDUE");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"⚠️ Bạn có **{overdueTasks.Count}** công việc đang bị quá hạn:");
            foreach (var t in overdueTasks)
            {
                var dateStr = t.Deadline?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
                sb.AppendLine($"• **{t.Title}** (Hạn chót: {dateStr}, Trạng thái: {t.Status})");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_OVERDUE");
        }

        if (lowerMsg.Contains("chờ xử lý") || lowerMsg.Contains("cần làm") || lowerMsg.Contains("đang làm") || lowerMsg.Contains("nhiệm vụ của tôi") || lowerMsg.Contains("tiến độ"))
        {
            if (pendingTasks.Count == 0)
            {
                var noPendingMsg = locale == "ja" ? "📋 処理待ちのタスクはありません。" : (locale == "en" ? "📋 You have no pending tasks." : "📋 Hiện tại bạn không có nhiệm vụ nào đang chờ xử lý.");
                return new RobotChatResponse(noPendingMsg, "QUERY_PENDING");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 Bạn đang có **{pendingTasks.Count}** nhiệm vụ đang tiến hành:");
            foreach (var t in pendingTasks.Take(5))
            {
                var dlStr = t.Deadline.HasValue ? $" - Hạn: {t.Deadline.Value.ToLocalTime():dd/MM/yyyy}" : "";
                sb.AppendLine($"• **{t.Title}** [{t.Priority}] (Trạng thái: {t.Status}{dlStr})");
            }
            if (pendingTasks.Count > 5)
            {
                sb.AppendLine($"... và còn {pendingTasks.Count - 5} công việc khác.");
            }
            return new RobotChatResponse(sb.ToString().TrimEnd(), "QUERY_PENDING");
        }

        // Long-term enterprise vector memory RAG check for policies and guidelines
        if (vectorMemoryStore != null && (lowerMsg.Contains("quy định") || lowerMsg.Contains("chính sách") || lowerMsg.Contains("nội quy") || lowerMsg.Contains("tiêu chuẩn") || lowerMsg.Contains("hướng dẫn")))
        {
            var memoryMatches = await vectorMemoryStore.SearchMemoriesAsync(tenantContext.TenantId!.Value, message, null, 2, 0.35f, cancellationToken);
            if (memoryMatches.Count > 0)
            {
                var msb = new StringBuilder();
                msb.AppendLine("📖 **Căn cứ theo Tri thức & Quy định doanh nghiệp (RAG):**");
                foreach (var match in memoryMatches)
                {
                    msb.AppendLine($"\n📌 **{match.Title}** [{match.MemoryScope}]:\n{match.Content}");
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

                var targetLanguage = locale switch
                {
                    "ja" => "tiếng Nhật (Japanese)",
                    "en" => "tiếng Anh (English)",
                    _ => "tiếng Việt (Vietnamese)"
                };

                var toolsSummary = string.Join("\n", (toolRegistry?.GetRegisteredTools() ?? []).Select(t => $"- {t.Name}: {t.Description}"));

                var sysPrompt = $$"""
                    Bạn là Trợ lý Robot AI thông minh của nền tảng BizFlow Platform.
                    Trả lời người dùng bằng {{targetLanguage}}, ngắn gọn, thân thiện, lịch sự và súc tích.
                    QUY TẮC BẮT BUỘC (BR-027):
                    Chỉ dựa vào dữ liệu thực tế sau đây được trích xuất từ database của người dùng:
                    {{groundingData}}
                    Tuyệt đối không bịa đặt nhiệm vụ, deadline, phòng ban hay số liệu không có trong dữ liệu trên.
                    Nếu thông tin không có, nói rõ là không tìm thấy trong phạm vi dữ liệu hiện tại.

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
                            $"⚠️ Công cụ '{toolName}' không nằm trong danh mục công cụ được cấp phép.",
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
                                $"{reply}\n\n⚠️ Đề xuất hành động '{toolName}' cần sự xác nhận của bạn trước khi thực hiện.",
                                "PROPOSE_ACTION",
                                [
                                    new RobotActionCard(
                                        "CONFIRM_ACTION",
                                        $"Xác nhận {toolName}",
                                        "/api/v1/ai/actions/execute",
                                        new { toolName, confirmationToken = actionResult.ConfirmationToken, argsJson })
                                ]);
                        }

                        return new RobotChatResponse($"{reply}\n\n✅ Đã thực hiện thành công.", "EXECUTE_ACTION");
                    }
                    catch (ApplicationFault af)
                    {
                        return new RobotChatResponse($"❌ Không thể thực hiện hành động: {af.Message}", "ACTION_REJECTED");
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

        // Deterministic General Assistance with localization
        var genSb = new StringBuilder();
        if (locale == "ja")
        {
            genSb.AppendLine("こんにちは！BizFlow Robot アシスタントです 🤖。");
            genSb.AppendLine();
            genSb.AppendLine($"現在の概要: 進行中 **{pendingTasks.Count}** 件、本日期限 **{dueTodayTasks.Count}** 件、期限超過 **{overdueTasks.Count}** 件。");
            genSb.AppendLine();
            genSb.AppendLine("質問の例:");
            genSb.AppendLine("• *\"本日期限のタスクは？\"*");
            genSb.AppendLine("• *\"期限超過のタスクを確認\"*");
            genSb.AppendLine("• または `/task <内容>` コマンドでタスクをAI分解します。");
            genSb.AppendLine("• `/notifications` で通知とリマインダーを確認します。");
        }
        else if (locale == "en")
        {
            genSb.AppendLine("Hello! I am your BizFlow Robot Assistant 🤖.");
            genSb.AppendLine();
            genSb.AppendLine($"Your current overview: **{pendingTasks.Count}** in progress, **{dueTodayTasks.Count}** due today, **{overdueTasks.Count}** overdue.");
            genSb.AppendLine();
            genSb.AppendLine("You can ask me:");
            genSb.AppendLine("• *\"What deadlines do I have today?\"*");
            genSb.AppendLine("• *\"Check my overdue tasks\"*");
            genSb.AppendLine("• Or use `/task <description>` to break down work with AI.");
            genSb.AppendLine("• Use `/notifications` to check notifications and reminders.");
        }
        else
        {
            genSb.AppendLine("Chào bạn! Tôi là Trợ lý Robot BizFlow 🤖.");
            genSb.AppendLine();
            genSb.AppendLine($"Tổng quan hiện tại của bạn: **{pendingTasks.Count}** việc đang tiến hành, **{dueTodayTasks.Count}** việc đến hạn hôm nay, **{overdueTasks.Count}** việc quá hạn.");
            genSb.AppendLine();
            genSb.AppendLine("Bạn có thể hỏi tôi:");
            genSb.AppendLine("• *\"Hôm nay tôi có deadline nào?\"*");
            genSb.AppendLine("• *\"Kiểm tra các việc bị quá hạn\"*");
            genSb.AppendLine("• *\"Liệt kê các việc chờ xử lý của tôi\"*");
            genSb.AppendLine("• Hoặc dùng lệnh `/task <nội dung>` để phân rã công việc thông minh.");
            genSb.AppendLine("• Dùng lệnh `/create-task <tiêu đề>` để đề xuất tạo công việc mới.");
            genSb.AppendLine("• Dùng lệnh `/notifications` để xem thông báo và nhắc hạn.");
        }

        return new RobotChatResponse(genSb.ToString(), "GENERAL");
    }
}
