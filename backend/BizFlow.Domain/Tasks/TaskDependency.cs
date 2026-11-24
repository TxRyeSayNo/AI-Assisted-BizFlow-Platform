using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tasks;

/// <summary>
/// "This task waits for that one" (F7). The dependency is advisory on purpose: it never blocks a
/// transition, because the task state machine stays exactly as it was. What it does is tell the
/// people involved, and the task detail view, that a predecessor is still open.
///
/// Removing a link is a soft change, like every other collaboration record on the platform, so the
/// history of what once waited for what is not rewritten.
/// </summary>
public sealed class TaskDependency
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The task that waits.</summary>
    public Guid TaskId { get; private set; }

    /// <summary>The task that must finish first.</summary>
    public Guid DependsOnTaskId { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    private TaskDependency() { }

    public static TaskDependency Create(Guid tenantId, Guid taskId, Guid dependsOnTaskId, Guid createdBy,
        DateTimeOffset now)
    {
        var waits = EntityRules.Id(taskId, nameof(taskId));
        var first = EntityRules.Id(dependsOnTaskId, nameof(dependsOnTaskId));
        if (waits == first)
            throw new ArgumentException("A task cannot wait for itself.", nameof(dependsOnTaskId));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            TaskId = waits,
            DependsOnTaskId = first,
            CreatedBy = EntityRules.Id(createdBy, nameof(createdBy)),
            CreatedAt = now
        };
    }

    public bool Remove(DateTimeOffset now)
    {
        if (RemovedAt is not null) return false;
        RemovedAt = now;
        return true;
    }

    /// <summary>
    /// Re-links a dependency that had been taken off. Only <see cref="RemovedAt"/> moves, because
    /// identity and creation stamps are immutable here as everywhere else.
    /// </summary>
    public bool Restore()
    {
        if (RemovedAt is null) return false;
        RemovedAt = null;
        return true;
    }
}

/// <summary>
/// Which task states count as "done" for the purpose of a dependency warning. Kept here, next to
/// the record, so the rule is stated once and the state machine is never consulted for permission.
/// </summary>
public static class TaskDependencyRules
{
    /// <summary>A predecessor in one of these states no longer holds anything up.</summary>
    public static bool IsSettled(TaskState state) =>
        state is TaskState.Confirmed or TaskState.Completed or TaskState.Cancelled;

    /// <summary>
    /// Walks the existing links to decide whether adding <paramref name="dependsOnTaskId"/> as a
    /// predecessor of <paramref name="taskId"/> would close a loop. <paramref name="edges"/> maps a
    /// waiting task to the tasks it already waits for.
    /// </summary>
    public static bool WouldCycle(Guid taskId, Guid dependsOnTaskId,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges)
    {
        if (taskId == dependsOnTaskId) return true;
        // Walk forward from the proposed predecessor: if the waiting task is reachable, the new
        // edge would close a loop.
        var seen = new HashSet<Guid> { dependsOnTaskId };
        var queue = new Queue<Guid>();
        queue.Enqueue(dependsOnTaskId);
        while (queue.Count > 0)
        {
            if (!edges.TryGetValue(queue.Dequeue(), out var next)) continue;
            foreach (var candidate in next)
            {
                if (candidate == taskId) return true;
                if (seen.Add(candidate)) queue.Enqueue(candidate);
            }
        }
        return false;
    }
}
