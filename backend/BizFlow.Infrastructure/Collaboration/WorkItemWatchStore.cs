using BizFlow.Application.Collaboration;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Requests;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

/// <summary>
/// Storage for watchers and mentions (F3). Visibility reuses the very same internal gates the
/// task and request readers use, so this store cannot widen what a caller may see, and every
/// query runs under the fail-closed tenant filters already configured on the context.
/// </summary>
public sealed class WorkItemWatchStore(BizFlowDbContext db) : IWorkItemWatchStore
{
    public Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken) =>
        TaskReadQueries.Visible(db, scope).AnyAsync(t => t.Id == taskId, cancellationToken);

    public Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken) =>
        RequestReadQueries.Visible(db, scope).AnyAsync(r => r.Id == requestId, cancellationToken);

    public async Task<(bool Watching, int Count)> ReadStatusAsync(Guid tenantId, CommentObjectType objectType,
        Guid objectId, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.WorkItemWatchers.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.ObjectType == objectType && w.ObjectId == objectId &&
                w.RemovedAt == null)
            .Select(w => w.UserId)
            .ToListAsync(cancellationToken);
        return (rows.Contains(userId), rows.Count);
    }

    public async Task<bool> AddWatcherAsync(WorkItemWatcher watcher, CancellationToken cancellationToken)
    {
        // The unique index means a person has at most one row per item, active or cancelled.
        var existing = await db.WorkItemWatchers.SingleOrDefaultAsync(w => w.TenantId == watcher.TenantId &&
            w.ObjectType == watcher.ObjectType && w.ObjectId == watcher.ObjectId && w.UserId == watcher.UserId,
            cancellationToken);
        if (existing is not null)
        {
            if (!existing.Resume(watcher.CreatedAt)) return false;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        db.WorkItemWatchers.Add(watcher);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveWatcherAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.WorkItemWatchers.SingleOrDefaultAsync(w => w.TenantId == tenantId &&
            w.ObjectType == objectType && w.ObjectId == objectId && w.UserId == userId, cancellationToken);
        if (existing is null || !existing.Remove(now)) return false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<Comment?> FindCommentAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid commentId, CancellationToken cancellationToken) =>
        db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == commentId &&
            c.ObjectType == objectType && c.ObjectId == objectId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ActiveTenantUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> candidates,
        CancellationToken cancellationToken)
    {
        var ids = candidates.ToArray();
        return await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && ids.Contains(u.Id) && u.Status == UserStatus.Active)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ExistingMentionsAsync(Guid tenantId, Guid commentId,
        CancellationToken cancellationToken) =>
        await db.CommentMentions.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.CommentId == commentId)
            .Select(m => m.MentionedUserId)
            .ToListAsync(cancellationToken);

    public async Task CommitMentionsAsync(IReadOnlyCollection<CommentMention> mentions,
        IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        db.CommentMentions.AddRange(mentions);
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
    }
}
