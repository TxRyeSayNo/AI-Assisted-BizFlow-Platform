using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Notifications;

public sealed class NotificationInboxStore(BizFlowDbContext db, ITenantContext context) : INotificationInboxStore
{
    private void AssertRecipient(Guid tenantId, Guid recipientId)
    {
        if (tenantId == Guid.Empty || recipientId == Guid.Empty || context.TenantId != tenantId || context.UserId != recipientId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Notification access is restricted to its tenant recipient.");
    }
    public async Task<NotificationPage> ListAsync(Guid tenantId, Guid recipientId, NotificationFilter filter, CancellationToken cancellationToken)
    {
        AssertRecipient(tenantId, recipientId);
        var own = db.Notifications.AsNoTracking().Where(n => n.TenantId == tenantId && n.RecipientId == recipientId);
        var unread = await own.LongCountAsync(n => n.ReadAt == null, cancellationToken);
        var query = filter.UnreadOnly ? own.Where(n => n.ReadAt == null) : own;
        var total = await query.LongCountAsync(cancellationToken);
        // Deterministic time-based UUIDv7 order; SentAt is email delivery, not event creation.
        var rows = await query.OrderByDescending(n => n.Id).Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(n => new NotificationRow(n.Id, n.Type, n.ObjectType, n.ObjectId, n.Title, n.Content, n.ReadAt, n.SentAt)).ToListAsync(cancellationToken);
        return new(rows, filter.Page, filter.PageSize, total, unread);
    }
    public async Task<INotificationReceiptTransaction?> BeginReadAsync(Guid tenantId, Guid recipientId, Guid id, CancellationToken cancellationToken)
    {
        AssertRecipient(tenantId, recipientId);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var notification = await db.Notifications.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Notification" WHERE "NotificationId"={id}
                AND "TenantId"={tenantId} AND "RecipientId"={recipientId} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (notification is null) { await transaction.DisposeAsync(); return null; }
            return new ReceiptTransaction(db, transaction, notification);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class ReceiptTransaction(BizFlowDbContext db, IDbContextTransaction transaction, Notification notification) : INotificationReceiptTransaction
    {
        public Notification Notification => notification;
        public async Task<NotificationRow> CommitAsync(AuditLog? audit, CancellationToken cancellationToken)
        {
            if (audit is not null) db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(notification.Id, notification.Type, notification.ObjectType, notification.ObjectId,
                notification.Title, notification.Content, notification.ReadAt, notification.SentAt);
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
