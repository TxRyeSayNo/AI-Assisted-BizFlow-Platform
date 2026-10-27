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
    IAiModelClient? modelClient = null) : IRobotChatService
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

        // 3. Natural Language Task and Deadline Query with RBAC & Anti-Hallucination
        return await HandleNaturalLanguageQueryAsync(message, cancellationToken);
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
            [new RobotActionCard("OPEN_DECOMPOSITION", "Xem & Xác nhận tạo việc", "/tasks/ai-decomposition", new { prompt })]);
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

    private async Task<RobotChatResponse> HandleNaturalLanguageQueryAsync(string message, CancellationToken cancellationToken)
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
        if (lowerMsg.Contains("hôm nay") || lowerMsg.Contains("today") || (lowerMsg.Contains("deadline") && !lowerMsg.Contains("quá hạn")))
        {
            if (dueTodayTasks.Count == 0)
            {
                return new RobotChatResponse(
                    "📅 Hôm nay bạn không có deadline nào cần hoàn thành. Chúc bạn một ngày làm việc hiệu quả!",
                    "QUERY_DEADLINE");
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
                return new RobotChatResponse(
                    "🎉 Tuyệt vời! Hiện tại bạn không có công việc nào bị quá hạn.",
                    "QUERY_OVERDUE");
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
                return new RobotChatResponse(
                    "📋 Hiện tại bạn không có nhiệm vụ nào đang chờ xử lý.",
                    "QUERY_PENDING");
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

        // 3. Fallback / General Query with LLM Grounding or Friendly Help
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
                        t.Title,
                        t.Status,
                        t.Priority,
                        Deadline = t.Deadline?.ToString("dd/MM/yyyy HH:mm")
                    })
                });

                var sysPrompt = $"""
                    Bạn là Trợ lý Robot AI thông minh của nền tảng BizFlow Platform.
                    Trả lời người dùng bằng tiếng Việt, ngắn gọn, thân thiện, lịch sự và súc tích.
                    QUY TẮC BẮT BUỘC (BR-027):
                    Chỉ dựa vào dữ liệu thực tế sau đây được trích xuất từ database của người dùng:
                    {groundingData}
                    Tuyệt đối không bịa đặt nhiệm vụ, deadline, phòng ban hay số liệu không có trong dữ liệu trên.
                    Nếu thông tin không có, nói rõ là không tìm thấy trong phạm vi dữ liệu hiện tại.
                    """;

                var aiResp = await modelClient.CompleteAsync(message, sysPrompt, cancellationToken);
                return new RobotChatResponse(aiResp.Content, "GENERAL");
            }
            catch
            {
                // Fallback to deterministic summary if LLM call fails
            }
        }

        // Deterministic General Assistance
        var genSb = new StringBuilder();
        genSb.AppendLine("Chào bạn! Tôi là Trợ lý Robot BizFlow 🤖.");
        genSb.AppendLine();
        genSb.AppendLine($"Tổng quan hiện tại của bạn: **{pendingTasks.Count}** việc đang tiến hành, **{dueTodayTasks.Count}** việc đến hạn hôm nay, **{overdueTasks.Count}** việc quá hạn.");
        genSb.AppendLine();
        genSb.AppendLine("Bạn có thể hỏi tôi:");
        genSb.AppendLine("• *\"Hôm nay tôi có deadline nào?\"*");
        genSb.AppendLine("• *\"Kiểm tra các việc bị quá hạn\"*");
        genSb.AppendLine("• *\"Liệt kê các việc chờ xử lý của tôi\"*");
        genSb.AppendLine("• Hoặc dùng lệnh `/task <nội dung>` để phân rã công việc thông minh.");
        genSb.AppendLine("• Dùng lệnh `/notifications` để xem thông báo và nhắc hạn.");

        return new RobotChatResponse(genSb.ToString(), "GENERAL");
    }
}
