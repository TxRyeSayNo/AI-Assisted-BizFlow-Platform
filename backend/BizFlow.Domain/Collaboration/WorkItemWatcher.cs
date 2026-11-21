using BizFlow.Domain.Common;

namespace BizFlow.Domain.Collaboration;

/// <summary>
/// A person following a task or a request (F3). Watching grants nothing: the reader still has to
/// pass the task or request read policy every time, so a watcher who loses access simply stops
/// being notified and stops seeing the item.
/// </summary>
public sealed class WorkItemWatcher
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public CommentObjectType ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Watermark for the activity sweep: audit entries at or before this instant have already been
    /// notified to this watcher. It makes the sweep idempotent without a second table, so a job
    /// that runs twice never sends the same update twice.
    /// </summary>
    public DateTimeOffset? LastNotifiedAt { get; private set; }

    /// <summary>
    /// Unfollowing is a soft change. The platform forbids hard deletes outside a small set of link
    /// tables, so a cancelled subscription keeps its row and simply stops counting; following the
    /// item again revives that same row.
    /// </summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    private WorkItemWatcher() { }

    public static WorkItemWatcher Create(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid userId, DateTimeOffset now)
    {
        if (objectType is not (CommentObjectType.Task or CommentObjectType.Request))
            throw new ArgumentException("Only a task or a request can be watched.", nameof(objectType));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            ObjectType = objectType,
            ObjectId = EntityRules.Id(objectId, nameof(objectId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            CreatedAt = now,
            // Start from the moment of subscription: a new watcher is not told about old history.
            LastNotifiedAt = now
        };
    }

    public bool Advance(DateTimeOffset to)
    {
        if (LastNotifiedAt is { } seen && seen >= to) return false;
        LastNotifiedAt = to;
        return true;
    }

    /// <summary>Stops following. Returns false when it was already stopped.</summary>
    public bool Remove(DateTimeOffset now)
    {
        if (RemovedAt is not null) return false;
        RemovedAt = now;
        return true;
    }

    /// <summary>Follows again, and does not replay anything that happened while away.</summary>
    public bool Resume(DateTimeOffset now)
    {
        if (RemovedAt is null) return false;
        RemovedAt = null;
        LastNotifiedAt = now;
        return true;
    }
}
