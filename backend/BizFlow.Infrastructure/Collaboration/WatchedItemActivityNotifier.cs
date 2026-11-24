using BizFlow.Application.Notifications;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Notifications;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Collaboration;

public interface IWatchedItemActivityNotifier
{
    /// <summary>Returns how many notifications were created across all tenants.</summary>
    Task<int> SweepAsync(CancellationToken cancellationToken);

    /// <summary>Sweeps a single tenant. Separated so it can be driven directly by a test.</summary>
    Task<int> SweepTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Notifies watchers about activity on items they follow (F3), by reading the existing
/// <c>AuditLog</c> and <c>Comment</c> history rather than by changing any command. That is the
/// non-invasive hook the plan asks for: no task or request command publishes anything new, and no
/// state machine is touched.
///
/// Idempotency comes from the watcher's own <c>LastNotifiedAt</c> watermark, so running the sweep
/// twice — or restarting mid-run — never sends the same activity twice.
///
/// Tenancy: the only cross-tenant read is the list of tenant ids, which is platform metadata that
/// no per-tenant filter can express. Every read of business data then happens through a context
/// scoped to one tenant, with the fail-closed query filters fully in force; nothing here calls
/// IgnoreQueryFilters.
/// </summary>
public sealed class WatchedItemActivityNotifier(
    DbContextOptions<BizFlowDbContext> options,
    INotificationUpdates updates,
    TimeProvider clock,
    ILogger<WatchedItemActivityNotifier> logger) : IWatchedItemActivityNotifier
{
    /// <summary>Never report more than this many events per watcher per sweep.</summary>
    public const int MaxEventsPerWatcher = 20;

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var created = 0;
        foreach (var tenantId in await ActiveTenantIdsAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                created += await SweepTenantAsync(tenantId, cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // One tenant's problem must not stop the others.
                logger.LogWarning(failure, "Watched item sweep failed for tenant {TenantId}.", tenantId);
            }
        }
        return created;
    }

    public async Task<int> SweepTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = new BizFlowDbContext(options, new JobContext(tenantId));
        var watchers = await db.WorkItemWatchers
            .Where(w => w.TenantId == tenantId && w.RemovedAt == null)
            .ToListAsync(cancellationToken);
        if (watchers.Count == 0) return 0;

        var now = clock.GetUtcNow();
        var notifications = new List<Notification>();
        var recipients = new HashSet<Guid>();
        foreach (var watcher in watchers)
        {
            var since = watcher.LastNotifiedAt ?? watcher.CreatedAt;
            var code = watcher.ObjectType == CommentObjectType.Task ? "TASK" : "REQUEST";

            // Status changes and other business events, straight from the audit trail.
            var events = await db.AuditLogs.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.ObjectType == code && a.ObjectId == watcher.ObjectId &&
                    a.CreatedAt > since && a.ActorId != watcher.UserId)
                .OrderBy(a => a.CreatedAt)
                .Take(MaxEventsPerWatcher)
                .Select(a => new { a.Action, a.CreatedAt })
                .ToListAsync(cancellationToken);

            // New comments by someone else on the watched item.
            var comments = await db.Comments.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.ObjectType == watcher.ObjectType &&
                    c.ObjectId == watcher.ObjectId && c.CreatedAt > since && c.AuthorId != watcher.UserId)
                .OrderBy(c => c.CreatedAt)
                .Take(MaxEventsPerWatcher)
                .Select(c => new { c.CreatedAt })
                .ToListAsync(cancellationToken);

            if (events.Count == 0 && comments.Count == 0) continue;

            var latest = events.Select(e => e.CreatedAt).Concat(comments.Select(c => c.CreatedAt)).Max();
            var summary = Summarize(events.Count, comments.Count, events.Select(e => e.Action).ToArray());
            notifications.Add(Notification.Create(tenantId, watcher.UserId, NotificationEvent.WatchedItemActivity,
                "Mục bạn theo dõi có cập nhật mới",
                summary,
                // The watermark is part of the key, so a replayed sweep cannot duplicate a row.
                $"watch:{watcher.Id}:{latest.ToUnixTimeMilliseconds()}",
                code, watcher.ObjectId));
            recipients.Add(watcher.UserId);
            watcher.Advance(latest);
        }

        if (notifications.Count == 0) return 0;
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await updates.PublishAsync(tenantId, [.. recipients], cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // The inbox row is already durable; a realtime hiccup must not fail the sweep.
            logger.LogWarning(failure, "Realtime publish failed after a watched item sweep for tenant {TenantId}.", tenantId);
        }
        logger.LogInformation("Watched item sweep notified {Count} watcher(s) in tenant {TenantId} at {At}.",
            notifications.Count, tenantId, now);
        return notifications.Count;
    }

    private static string Summarize(int eventCount, int commentCount, string[] actions)
    {
        var parts = new List<string>(2);
        if (eventCount > 0)
        {
            var last = actions.Length > 0 ? actions[^1] : null;
            parts.Add(last is null
                ? $"{eventCount} thay đổi trạng thái"
                : $"{eventCount} thay đổi trạng thái (mới nhất: {last})");
        }
        if (commentCount > 0) parts.Add($"{commentCount} bình luận mới");
        return string.Join(" · ", parts);
    }

    private async Task<IReadOnlyList<Guid>> ActiveTenantIdsAsync(CancellationToken cancellationToken)
    {
        // Platform metadata only: the identifiers of active tenants. No business row is read here,
        // and every later read runs inside a single tenant's query filters.
        await using var db = new BizFlowDbContext(options, new JobContext(null));
        return await db.Database
            .SqlQuery<Guid>($"SELECT \"TenantId\" AS \"Value\" FROM \"Tenant\" WHERE \"Status\" = 'ACTIVE'")
            .ToListAsync(cancellationToken);
    }

    /// <summary>A tenant context the job sets itself, so filters stay on instead of being bypassed.</summary>
    private sealed class JobContext(Guid? tenantId) : BizFlow.Application.Security.ITenantContext
    {
        // A system actor still has to be non-null for the fail-closed filters to admit anything.
        public Guid? UserId { get; } = tenantId is null ? null : SystemActorId;
        public Guid? TenantId { get; } = tenantId;
        internal static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");
    }
}
