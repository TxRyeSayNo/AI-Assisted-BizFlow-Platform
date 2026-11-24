using BizFlow.Application.Collaboration;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Requests;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

/// <summary>
/// Storage for the label catalogue and the labels attached to work items (F6). Visibility reuses
/// the same internal gates the task and request readers use, and every query runs under the
/// fail-closed tenant filters already configured on the context.
/// </summary>
public sealed class LabelStore(BizFlowDbContext db) : ILabelStore
{
    public async Task<IReadOnlyList<(Label Label, int UsageCount)>> ListAsync(Guid tenantId, bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = db.Labels.AsNoTracking().Where(l => l.TenantId == tenantId);
        if (!includeArchived) query = query.Where(l => l.ArchivedAt == null);
        var rows = await query
            .OrderBy(l => l.NormalizedName)
            .Select(l => new
            {
                Label = l,
                UsageCount = db.WorkItemLabels.Count(a => a.TenantId == tenantId && a.LabelId == l.Id && a.RemovedAt == null)
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => (row.Label, row.UsageCount))];
    }

    public Task<Label?> FindAsync(Guid tenantId, Guid labelId, CancellationToken cancellationToken) =>
        db.Labels.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.Id == labelId, cancellationToken);

    public Task<Label?> FindByNameAsync(Guid tenantId, string normalizedName, CancellationToken cancellationToken) =>
        db.Labels.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.NormalizedName == normalizedName,
            cancellationToken);

    public async Task AddAsync(Label label, CancellationToken cancellationToken)
    {
        db.Labels.Add(label);
        await Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken) =>
        TaskReadQueries.Visible(db, scope).AnyAsync(t => t.Id == taskId, cancellationToken);

    public Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken) =>
        RequestReadQueries.Visible(db, scope).AnyAsync(r => r.Id == requestId, cancellationToken);

    public async Task<IReadOnlyList<Label>> LabelsOnAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        CancellationToken cancellationToken) =>
        await db.WorkItemLabels.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.ObjectType == objectType && a.ObjectId == objectId &&
                a.RemovedAt == null)
            .Join(db.Labels.AsNoTracking().Where(l => l.TenantId == tenantId),
                a => a.LabelId, l => l.Id, (_, l) => l)
            .OrderBy(l => l.NormalizedName)
            .ToListAsync(cancellationToken);

    public Task<WorkItemLabel?> FindAttachmentAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid labelId, CancellationToken cancellationToken) =>
        db.WorkItemLabels.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.ObjectType == objectType &&
            a.ObjectId == objectId && a.LabelId == labelId, cancellationToken);

    public async Task AddAttachmentAsync(WorkItemLabel attachment, CancellationToken cancellationToken)
    {
        db.WorkItemLabels.Add(attachment);
        await Task.CompletedTask;
    }
}
