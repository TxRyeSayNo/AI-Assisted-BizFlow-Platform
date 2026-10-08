using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Notifications;

public sealed record NotificationFilter(int Page = 1, int PageSize = 25, bool UnreadOnly = false);
public sealed record NotificationRow(Guid NotificationId, string Type, string? ObjectType, Guid? ObjectId,
    string Title, string Content, DateTimeOffset? ReadAt, DateTimeOffset? SentAt);
public sealed record NotificationPage(IReadOnlyList<NotificationRow> Items, int Page, int PageSize, long Total, long UnreadCount);
public interface INotificationInboxStore
{
    Task<NotificationPage> ListAsync(Guid tenantId, Guid recipientId, NotificationFilter filter, CancellationToken cancellationToken);
    Task<INotificationReceiptTransaction?> BeginReadAsync(Guid tenantId, Guid recipientId, Guid id, CancellationToken cancellationToken);
}
public interface INotificationReceiptTransaction : IAsyncDisposable
{
    Notification Notification { get; }
    Task<NotificationRow> CommitAsync(AuditLog? audit, CancellationToken cancellationToken);
}

public sealed class NotificationInbox(ITenantContext context, TenantMembershipAuthorizer membership,
    ISecurityAuditWriter securityAudit, INotificationInboxStore store, TimeProvider clock, INotificationUpdates updates)
{
    public async Task<NotificationPage> ListAsync(NotificationFilter filter, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a positive page and a page size from 1 to 100.");
        return await store.ListAsync(tenantId, context.UserId!.Value, filter, cancellationToken);
    }

    public async Task<NotificationRow> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.mark-read", cancellationToken);
        var recipientId = context.UserId!.Value;
        await using var transaction = await store.BeginReadAsync(tenantId, recipientId, id, cancellationToken);
        if (transaction is null)
        {
            await securityAudit.RecordDeniedAccessAsync(recipientId, tenantId, "notifications.mark-read", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        await membership.RequireAsync("notifications.mark-read", cancellationToken);
        var changed = transaction.Notification.MarkRead(tenantId, recipientId, clock.GetUtcNow());
        var audit = changed ? AuditLog.NotificationRead(tenantId, recipientId, id, transaction.Notification.ReadAt!.Value) : null;
        var result = await transaction.CommitAsync(audit, cancellationToken);
        if (changed) await updates.PublishAsync(tenantId, [recipientId], cancellationToken);
        return result;
    }
}
