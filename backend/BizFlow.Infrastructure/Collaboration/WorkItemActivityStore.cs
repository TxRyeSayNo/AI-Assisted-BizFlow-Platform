using BizFlow.Application.Collaboration;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Requests;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

/// <summary>
/// Visibility and actor names for the in-item activity feed (F11). The audit rows themselves come
/// from the existing audit reader, so there is no second query path over the audit trail here.
/// </summary>
public sealed class WorkItemActivityStore(BizFlowDbContext db) : IWorkItemActivityStore
{
    public Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken) =>
        TaskReadQueries.Visible(db, scope).AnyAsync(t => t.Id == taskId, cancellationToken);

    public Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken) =>
        RequestReadQueries.Visible(db, scope).AnyAsync(r => r.Id == requestId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> ActorNamesAsync(Guid tenantId,
        IReadOnlyCollection<Guid> actorIds, CancellationToken cancellationToken)
    {
        var ids = actorIds.ToArray();
        var rows = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => row.FullName);
    }
}
