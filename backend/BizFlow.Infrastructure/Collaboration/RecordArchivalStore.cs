using BizFlow.Application.Collaboration;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class RecordArchivalStore(BizFlowDbContext db) : IRecordArchivalStore
{
    public async Task<WorkTask?> FindTaskForArchivalAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        return await db.WorkTasks
            .SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId, cancellationToken);
    }

    public async Task<bool> IsTaskArchivedAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
    {
        return await db.WorkTasks
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && t.Id == taskId && t.DeletedAt != null, cancellationToken);
    }

    public async Task ArchiveTaskAsync(WorkTask task, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkRequest?> FindRequestForArchivalAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        return await db.Requests
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == requestId, cancellationToken);
    }

    public async Task<bool> IsRequestArchivedAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        return await db.Requests
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId && r.Id == requestId && r.DeletedAt != null, cancellationToken);
    }

    public async Task ArchiveRequestAsync(WorkRequest request, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }
}
