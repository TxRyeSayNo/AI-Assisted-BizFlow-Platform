using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Notifications;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class AgentMeetingCoordinator(
    BizFlowDbContext db,
    INotificationUpdates notificationUpdates,
    IEmailNotificationDispatcher emailDispatcher,
    ILogger<AgentMeetingCoordinator> logger,
    TimeProvider clock) : IAgentMeetingCoordinator
{
    public async Task<MeetingDispatchResultDto> DispatchMeetingNotificationAsync(
        Guid tenantId,
        Guid senderUserId,
        string meetingTopic,
        string meetingTime,
        string meetingLocation,
        IReadOnlyList<string> participatingDepartments,
        IReadOnlyList<string>? logistics,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch all active departments in tenant
        var allDepts = await db.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .ToListAsync(cancellationToken);

        // 2. Resolve matched departments
        var matchedDepts = new List<Department>();
        if (participatingDepartments is { Count: > 0 })
        {
            foreach (var reqName in participatingDepartments)
            {
                var cleanReq = reqName.Trim().ToLowerInvariant();
                var matched = allDepts.FirstOrDefault(d =>
                    d.Name.ToLowerInvariant().Contains(cleanReq) ||
                    d.Code.ToLowerInvariant().Equals(cleanReq) ||
                    cleanReq.Contains(d.Name.ToLowerInvariant()) ||
                    cleanReq.Contains(d.Code.ToLowerInvariant()) ||
                    (cleanReq.Contains("it") && d.Code.Contains("TECH")) ||
                    (cleanReq.Contains("hr") && (d.Code.Contains("HR") || d.Name.Contains("Nhân sự"))) ||
                    (cleanReq.Contains("bgd") && (d.Code.Contains("BGD") || d.Name.Contains("Giám đốc"))) ||
                    (cleanReq.Contains("biz") && (d.Code.Contains("BIZ") || d.Name.Contains("Kinh doanh"))) ||
                    (cleanReq.Contains("fin") && (d.Code.Contains("FIN") || d.Name.Contains("Tài chính"))));

                if (matched != null && !matchedDepts.Contains(matched))
                {
                    matchedDepts.Add(matched);
                }
            }
        }

        // Fallback: If no match found or empty, include all active departments
        if (matchedDepts.Count == 0)
        {
            matchedDepts = allDepts;
        }

        // 3. For each matched department, resolve the manager or representative user
        var contacts = new List<DepartmentManagerContactDto>();
        var deptIds = matchedDepts.Select(d => d.Id).ToList();

        // Check management scopes for department managers
        var scopes = await db.ManagementScopes
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && deptIds.Contains(m.DepartmentId))
            .ToListAsync(cancellationToken);

        var scopeUserIds = scopes.Select(s => s.UserId).Distinct().ToList();
        var scopeUsers = await db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && scopeUserIds.Contains(u.Id) && u.Status == UserStatus.Active)
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        // Active users in departments for fallback
        var allDeptUsers = await db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.DepartmentId.HasValue && deptIds.Contains(u.DepartmentId.Value) && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        // Tenant admins for ultimate fallback
        var adminUsers = await db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsPlatformAdministrator && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var dept in matchedDepts)
        {
            // Try management scope first
            var deptScope = scopes.FirstOrDefault(s => s.DepartmentId == dept.Id);
            if (deptScope != null && scopeUsers.TryGetValue(deptScope.UserId, out var managerUser))
            {
                contacts.Add(new DepartmentManagerContactDto(
                    dept.Id, dept.Code, dept.Name, managerUser.Id, managerUser.FullName, managerUser.Email));
                continue;
            }

            // Fallback to active department user
            var deptUser = allDeptUsers.FirstOrDefault(u => u.DepartmentId == dept.Id);
            if (deptUser != null)
            {
                contacts.Add(new DepartmentManagerContactDto(
                    dept.Id, dept.Code, dept.Name, deptUser.Id, deptUser.FullName, deptUser.Email));
                continue;
            }

            // Fallback to admin user
            var adminUser = adminUsers.FirstOrDefault();
            if (adminUser != null)
            {
                contacts.Add(new DepartmentManagerContactDto(
                    dept.Id, dept.Code, dept.Name, adminUser.Id, adminUser.FullName, adminUser.Email));
            }
        }

        // Deduplicate contacts by ManagerUserId so we don't spam a user managing multiple departments
        var uniqueContacts = contacts
            .GroupBy(c => c.ManagerUserId)
            .Select(g => g.First())
            .ToList();

        // 4. Create in-app notifications and dispatch emails
        var now = clock.GetUtcNow();
        var logisticsNote = logistics is { Count: > 0 } ? $"\n• Hậu cần & Thiết bị: {string.Join(", ", logistics)}" : "";

        foreach (var contact in uniqueContacts)
        {
            var notif = Notification.Create(
                tenantId: tenantId,
                recipientId: contact.ManagerUserId,
                type: NotificationEvent.ApprovalRequired,
                title: $"[Thư mời họp] {meetingTopic}",
                content: $"Kính gửi {contact.ManagerName} ({contact.DepartmentName}),\n\nQuản trị viên đã triệu tập cuộc họp doanh nghiệp:\n• Thời gian: {meetingTime}\n• Địa điểm: {meetingLocation}\n• Nội dung: {meetingTopic}{logisticsNote}\n\nYêu cầu Quản lý đơn vị chuẩn bị tài liệu báo cáo và phân công nhân sự tham gia đầy đủ đúng giờ.",
                idempotencyKey: $"meet-{tenantId:N}-{contact.ManagerUserId:N}-{now.Ticks}");

            db.Notifications.Add(notif);

            try
            {
                await emailDispatcher.SendDeadlineEmailAsync(
                    tenantId,
                    contact.ManagerUserId,
                    $"[Thư mời họp] {meetingTopic} - {meetingTime} tại {meetingLocation}",
                    null,
                    "MEETING_INVITATION",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send email invitation to {Email}", contact.ManagerEmail);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // 5. Trigger SignalR realtime push to all recipient managers
        var recipientIds = uniqueContacts.Select(c => c.ManagerUserId).ToList();
        if (recipientIds.Count > 0)
        {
            try
            {
                await notificationUpdates.PublishAsync(tenantId, recipientIds, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish SignalR realtime notification updates");
            }
        }

        logger.LogInformation(
            "Agent successfully dispatched meeting notification for '{Topic}' to {Count} unit managers.",
            meetingTopic,
            uniqueContacts.Count);

        return new MeetingDispatchResultDto(
            InAppNotificationsSent: uniqueContacts.Count,
            EmailsSent: uniqueContacts.Count,
            Recipients: uniqueContacts);
    }
}

