using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class RequestSplitStore(BizFlowDbContext dbContext) : IRequestSplitStore
{
    public async Task<WorkRequest?> GetParentRequestAsync(
        Guid tenantId,
        Guid parentRequestId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Requests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == parentRequestId && r.DeletedAt == null, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkRequest>> CreateChildRequestsAsync(
        Guid tenantId,
        Guid parentRequestId,
        IReadOnlyList<WorkRequest> childRequests,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        foreach (var child in childRequests)
        {
            dbContext.Requests.Add(child);
        }

        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);

        return childRequests;
    }
}
