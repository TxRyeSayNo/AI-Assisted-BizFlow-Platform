using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

/// <summary>F8/F9 storage: the task template catalogue. Runs under the context's tenant filters.</summary>
public sealed class TaskTemplateStore(BizFlowDbContext db) : ITaskTemplateStore
{
    public async Task<IReadOnlyList<TaskTemplate>> ListAsync(Guid tenantId, bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = db.TaskTemplates.AsNoTracking().Where(t => t.TenantId == tenantId);
        if (!includeArchived) query = query.Where(t => t.ArchivedAt == null);
        return await query.OrderBy(t => t.NormalizedName).ToListAsync(cancellationToken);
    }

    public Task<TaskTemplate?> FindAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken) =>
        db.TaskTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == templateId, cancellationToken);

    public Task<TaskTemplate?> FindByNameAsync(Guid tenantId, string normalizedName,
        CancellationToken cancellationToken) =>
        db.TaskTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.NormalizedName == normalizedName,
            cancellationToken);

    public async Task AddAsync(TaskTemplate template, CancellationToken cancellationToken)
    {
        db.TaskTemplates.Add(template);
        await Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>F12 storage: one person's saved list filters.</summary>
public sealed class SavedFilterStore(BizFlowDbContext db) : ISavedFilterStore
{
    public async Task<IReadOnlyList<SavedFilter>> ListAsync(Guid tenantId, Guid userId, string scope,
        CancellationToken cancellationToken) =>
        await db.SavedFilters.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId && f.Scope == scope && f.RemovedAt == null)
            .OrderBy(f => f.NormalizedName)
            .ToListAsync(cancellationToken);

    public Task<SavedFilter?> FindAsync(Guid tenantId, Guid userId, Guid savedFilterId,
        CancellationToken cancellationToken) =>
        db.SavedFilters.SingleOrDefaultAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.Id == savedFilterId, cancellationToken);

    public Task<SavedFilter?> FindByNameAsync(Guid tenantId, Guid userId, string scope, string normalizedName,
        CancellationToken cancellationToken) =>
        db.SavedFilters.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.UserId == userId &&
            f.Scope == scope && f.NormalizedName == normalizedName, cancellationToken);

    public async Task<int> CountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await db.SavedFilters.AsNoTracking()
            .CountAsync(f => f.TenantId == tenantId && f.UserId == userId && f.RemovedAt == null,
                cancellationToken);

    public async Task AddAsync(SavedFilter filter, CancellationToken cancellationToken)
    {
        db.SavedFilters.Add(filter);
        await Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
