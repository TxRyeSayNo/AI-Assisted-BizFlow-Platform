using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Audit;

public sealed class SecurityAuditWriter(DbContextOptions<BizFlowDbContext> options, ITenantContext context,
    TimeProvider clock) : ISecurityAuditWriter
{
    public async Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission,
        AccessDenial reason, CancellationToken cancellationToken)
    {
        // Isolate the security event from any pending/failed business change tracker or transaction.
        await using var auditDb = new BizFlowDbContext(options, context);
        auditDb.AuditLogs.Add(AuditLog.DeniedAccess(tenantId, userId, permission, reason.ToString(), clock.GetUtcNow()));
        await auditDb.SaveChangesAsync(cancellationToken);
    }
}
