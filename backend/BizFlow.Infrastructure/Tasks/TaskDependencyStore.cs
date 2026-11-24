using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

/// <summary>
/// Storage for task dependencies (F7). Visibility reuses the very gate the task reader uses, and
/// every query runs under the fail-closed tenant filters already configured on the context.
/// </summary>
public sealed class TaskDependencyStore(BizFlowDbContext db) : ITaskDependencyStore
{
    public Task<bool> IsVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken) =>
        TaskReadQueries.Visible(db, scope).AnyAsync(t => t.Id == taskId, cancellationToken);

    public async Task<(IReadOnlyList<DependencyLink> DependsOn, IReadOnlyList<DependencyLink> Blocks)> LinksAsync(
        Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        // Both directions in one round trip: the link rows carry which end is which.
        var rows = await db.TaskDependencies.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.RemovedAt == null &&
                (d.TaskId == taskId || d.DependsOnTaskId == taskId))
            .Join(db.WorkTasks.AsNoTracking().Where(t => t.TenantId == tenantId),
                d => d.TaskId == taskId ? d.DependsOnTaskId : d.TaskId, t => t.Id,
                (d, t) => new { Forward = d.TaskId == taskId, t.Id, t.Title, t.Status, t.Deadline })
            .ToListAsync(cancellationToken);

        static DependencyLink Map(Guid id, string title, TaskState status, DateTimeOffset? deadline) =>
            new(id, title, status.ToString().ToUpperInvariant(), TaskDependencyRules.IsSettled(status), deadline);

        return (
            [.. rows.Where(r => r.Forward).OrderBy(r => r.Title)
                .Select(r => Map(r.Id, r.Title, r.Status, r.Deadline))],
            [.. rows.Where(r => !r.Forward).OrderBy(r => r.Title)
                .Select(r => Map(r.Id, r.Title, r.Status, r.Deadline))]);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> EdgesAsync(Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await db.TaskDependencies.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.RemovedAt == null)
            .Select(d => new { d.TaskId, d.DependsOnTaskId })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.TaskId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<Guid>)[.. group.Select(r => r.DependsOnTaskId)]);
    }

    public async Task<int> CountForAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken) =>
        await db.TaskDependencies.AsNoTracking()
            .CountAsync(d => d.TenantId == tenantId && d.TaskId == taskId && d.RemovedAt == null,
                cancellationToken);

    public Task<TaskDependency?> FindAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId,
        CancellationToken cancellationToken) =>
        db.TaskDependencies.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.TaskId == taskId &&
            d.DependsOnTaskId == dependsOnTaskId, cancellationToken);

    public async Task AddAsync(TaskDependency dependency, CancellationToken cancellationToken)
    {
        db.TaskDependencies.Add(dependency);
        await Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
