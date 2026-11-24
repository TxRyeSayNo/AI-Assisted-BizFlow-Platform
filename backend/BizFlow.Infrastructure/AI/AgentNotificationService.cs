using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class AgentNotificationService(BizFlowDbContext db, IResourceAuthorizer authorizer,
    INotificationUpdates updates, TimeProvider clock, ILogger<AgentNotificationService> logger) : IAgentNotificationService
{
    private async Task<(AgentNotificationPreview Preview, List<Guid> Recipients)> ResolveAsync(
        Guid tenantId, string toolName, string argsJson, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("notifications.send", new(tenantId), cancellationToken: cancellationToken);
        if (toolName is not ("send_meeting_notification" or "send_department_notification"))
            throw new ApplicationFault(FaultKind.Validation, "AI.UNKNOWN_TOOL", "Công cụ gửi thông báo không hợp lệ.");
        var args = JsonNode.Parse(argsJson) as JsonObject ?? throw new ApplicationFault(FaultKind.Validation, "AI.INVALID_ARGUMENTS", "Tham số không hợp lệ.");
        var missing = AgentToolSchemas.MissingOrInvalid(toolName, args, clock.GetUtcNow());
        if (missing.Count > 0) throw new ApplicationFault(FaultKind.Validation, "AI.MISSING_INFORMATION", "Cần bổ sung: " + string.Join(", ", missing.Values));
        var allCompany = args["recipientScope"]!.GetValue<string>() == "ALL_COMPANY";
        var departmentIds = allCompany ? [] : args["departmentIds"]!.AsArray().Select(n => Guid.Parse(n!.GetValue<string>())).Distinct().ToList();
        var departments = await db.Departments.AsNoTracking().Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active && (allCompany || departmentIds.Contains(d.Id)))
            .Select(d => new { d.Id, d.Name }).ToListAsync(cancellationToken);
        if (!allCompany && departments.Count != departmentIds.Count)
            throw new ApplicationFault(FaultKind.Validation, "AI.INVALID_RECIPIENTS", "Có phòng ban không tồn tại hoặc không hoạt động trong doanh nghiệp hiện tại.");
        var recipients = await db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.DeletedAt == null && u.Status == UserStatus.Active &&
            (allCompany || (u.DepartmentId.HasValue && departmentIds.Contains(u.DepartmentId.Value))))
            .Select(u => u.Id).Distinct().ToListAsync(cancellationToken);
        if (recipients.Count == 0) throw new ApplicationFault(FaultKind.Validation, "AI.NO_RECIPIENTS", "Không có người nhận đang hoạt động trong phạm vi đã chọn.");
        var title = args["title"]?.ToString() ?? "Thông báo họp: " + args["topic"]!.GetValue<string>();
        var content = args["content"]?.ToString() ?? "Nội dung: " + args["topic"] + "\nThời gian: " + args["startsAt"] +
            "\nĐịa điểm: " + args["location"] + (args["preparation"] == null ? "" : "\nCần chuẩn bị: " + args["preparation"]);
        if (title.Length > 200 || content.Length > 10000) throw new ApplicationFault(FaultKind.Validation, "AI.NOTIFICATION_TOO_LONG", "Tiêu đề tối đa 200 ký tự, nội dung tối đa 10000 ký tự.");
        return (new(recipients.Count, allCompany ? ["Toàn thể công ty"] : departments.Select(d => d.Name).ToList(), title, content, recipients), recipients);
    }

    public async Task<AgentNotificationPreview> PreviewAsync(Guid tenantId, string toolName, string argsJson, CancellationToken cancellationToken = default) =>
        (await ResolveAsync(tenantId, toolName, argsJson, cancellationToken)).Preview;

    public async Task<AgentNotificationResult> SendAsync(Guid tenantId, Guid userId, string toolName, string argsJson, CancellationToken cancellationToken = default)
    {
        var (preview, recipients) = await ResolveAsync(tenantId, toolName, argsJson, cancellationToken);
        using var args = JsonDocument.Parse(argsJson);
        if (!args.RootElement.TryGetProperty("operationId", out var operation) || !operation.TryGetGuid(out var operationId) || operationId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "AI.OPERATION_REQUIRED", "Thiếu mã thực hiện thông báo.");
        var prefix = $"agent-notice:{operationId:N}:";
        if (!args.RootElement.TryGetProperty("recipientIds", out var snapshot) || snapshot.ValueKind != JsonValueKind.Array ||
            !snapshot.EnumerateArray().Select(id => id.GetGuid()).ToHashSet().SetEquals(recipients))
            throw new ApplicationFault(FaultKind.Conflict, "AI.RECIPIENTS_CHANGED", "Danh sách người nhận đã thay đổi. Vui lòng xem lại bản đề xuất trước khi gửi.");
        // The ordinary inbox filter only exposes the current recipient. This administrative query is
        // explicitly tenant scoped and is reached only after notifications.send authorization.
        var existing = await db.Notifications.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.IdempotencyKey.StartsWith(prefix)).Select(n => n.RecipientId).ToListAsync(cancellationToken);
        var pending = recipients.Except(existing).ToList();
        foreach (var recipient in pending)
            db.Notifications.Add(Notification.Create(tenantId, recipient,
                toolName == "send_meeting_notification" ? NotificationEvent.MeetingInvitation : NotificationEvent.Announcement,
                preview.Title, preview.Content, prefix + recipient.ToString("N")));
        await db.SaveChangesAsync(cancellationToken);
        if (pending.Count > 0)
        {
            try { await updates.PublishAsync(tenantId, pending, cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "In-app notifications saved; realtime refresh could not be delivered."); }
        }
        return new(pending.Count, recipients.Count - pending.Count, "IN_APP", operationId);
    }
}
