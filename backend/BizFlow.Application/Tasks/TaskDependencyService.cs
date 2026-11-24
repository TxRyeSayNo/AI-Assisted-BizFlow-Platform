using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

/// <summary>One end of a dependency, as the task detail view needs to show it.</summary>
public sealed record DependencyLink(Guid TaskId, string Title, string Status, bool Settled,
    DateTimeOffset? Deadline);

/// <summary>
/// What a task waits for and what waits on it. <paramref name="BlockedBy"/> counts the predecessors
/// that are still open — the number the warning is built from. It never prevents anything.
/// </summary>
public sealed record TaskDependencyView(Guid TaskId, IReadOnlyList<DependencyLink> DependsOn,
    IReadOnlyList<DependencyLink> Blocks, int BlockedBy);

public interface ITaskDependencyStore
{
    Task<bool> IsVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken);

    /// <summary>The tasks <paramref name="taskId"/> waits for, and the tasks waiting on it.</summary>
    Task<(IReadOnlyList<DependencyLink> DependsOn, IReadOnlyList<DependencyLink> Blocks)> LinksAsync(
        Guid tenantId, Guid taskId, CancellationToken cancellationToken);

    /// <summary>Every live link in the tenant, as waiting task → the tasks it waits for.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> EdgesAsync(Guid tenantId,
        CancellationToken cancellationToken);

    Task<int> CountForAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken);
    Task<TaskDependency?> FindAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId,
        CancellationToken cancellationToken);
    Task AddAsync(TaskDependency dependency, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Dependencies between tasks (F7).
///
/// The platform's task state machine is not touched: a dependency is advisory, so starting a task
/// whose predecessor is still open stays possible and simply carries a warning in the view. That is
/// deliberate — turning a dependency into a hard gate would mean changing
/// <c>TaskLifecyclePolicy</c>, and a missed link would then block real work.
///
/// Both ends are checked against the caller's own task read policy, so a link can only be drawn
/// between two tasks the caller can already open; and a new link that would close a loop is
/// refused, so the warning can always be computed.
/// </summary>
public sealed class TaskDependencyService(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    ITaskDependencyStore store,
    TimeProvider clock)
{
    /// <summary>A guard against someone wiring a whole backlog onto one task.</summary>
    public const int MaxDependenciesPerTask = 20;

    public async Task<TaskDependencyView> ReadAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var (tenantId, _, _) = await RequireVisibleAsync(taskId, cancellationToken);
        return await ViewAsync(tenantId, taskId, cancellationToken);
    }

    public async Task<TaskDependencyView> AddAsync(Guid taskId, Guid dependsOnTaskId,
        CancellationToken cancellationToken)
    {
        var (tenantId, actorId, scope) = await RequireVisibleAsync(taskId, cancellationToken);
        if (dependsOnTaskId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "DEPENDENCY.TARGET_REQUIRED",
                "Choose the task that has to finish first.");
        if (dependsOnTaskId == taskId)
            throw new ApplicationFault(FaultKind.Validation, "DEPENDENCY.SELF",
                "A task cannot wait for itself.");

        // The predecessor has to be visible too, or a link would leak the existence of work the
        // caller may not see.
        if (!await store.IsVisibleAsync(scope, dependsOnTaskId, cancellationToken))
        {
            await audit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.read",
                AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        var existing = await store.FindAsync(tenantId, taskId, dependsOnTaskId, cancellationToken);
        if (existing is { RemovedAt: null }) return await ViewAsync(tenantId, taskId, cancellationToken);

        if (await store.CountForAsync(tenantId, taskId, cancellationToken) >= MaxDependenciesPerTask)
            throw new ApplicationFault(FaultKind.Validation, "DEPENDENCY.TOO_MANY",
                $"A task can wait for at most {MaxDependenciesPerTask} other tasks.");

        var edges = await store.EdgesAsync(tenantId, cancellationToken);
        if (TaskDependencyRules.WouldCycle(taskId, dependsOnTaskId, edges))
            throw new ApplicationFault(FaultKind.Validation, "DEPENDENCY.CYCLE",
                "That link would make two tasks wait for each other.");

        if (existing is null)
            await store.AddAsync(
                TaskDependency.Create(tenantId, taskId, dependsOnTaskId, actorId, clock.GetUtcNow()),
                cancellationToken);
        else
            existing.Restore();
        await store.SaveAsync(cancellationToken);
        return await ViewAsync(tenantId, taskId, cancellationToken);
    }

    public async Task<TaskDependencyView> RemoveAsync(Guid taskId, Guid dependsOnTaskId,
        CancellationToken cancellationToken)
    {
        var (tenantId, _, _) = await RequireVisibleAsync(taskId, cancellationToken);
        var existing = await store.FindAsync(tenantId, taskId, dependsOnTaskId, cancellationToken);
        if (existing is not null && existing.Remove(clock.GetUtcNow()))
            await store.SaveAsync(cancellationToken);
        return await ViewAsync(tenantId, taskId, cancellationToken);
    }

    // -------------------------------------------------------------------------------------------

    private async Task<TaskDependencyView> ViewAsync(Guid tenantId, Guid taskId,
        CancellationToken cancellationToken)
    {
        var (dependsOn, blocks) = await store.LinksAsync(tenantId, taskId, cancellationToken);
        return new(taskId, dependsOn, blocks, dependsOn.Count(link => !link.Settled));
    }

    private async Task<(Guid TenantId, Guid ActorId, TaskReadScope Scope)> RequireVisibleAsync(
        Guid taskId, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "DEPENDENCY.TASK_REQUIRED",
                "A task id is required.");
        if (context.UserId is not { } userId || userId == Guid.Empty || context.TenantId is not { } tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        var access = await snapshots.ResolveAsync(userId, tenantId, cancellationToken);
        if (access is null || access.UserId != userId || access.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");

        if (TaskReadPolicy.Resolve(access) is not { Decision.Allowed: true, Scope: { } scope } ||
            !await store.IsVisibleAsync(scope, taskId, cancellationToken))
        {
            await audit.RecordDeniedAccessAsync(userId, tenantId, "tasks.read",
                AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        return (tenantId, userId, scope);
    }
}
