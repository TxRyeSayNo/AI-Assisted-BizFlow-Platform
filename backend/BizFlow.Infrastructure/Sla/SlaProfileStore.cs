using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Sla;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Sla;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Sla;

public sealed class SlaProfileStore(BizFlowDbContext db, ITenantContext context) : ISlaProfileStore
{
    private void AssertTenant(Guid tenantId)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || context.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA profiles require a tenant workspace.");
    }
    public async Task<SlaProfilePage> ListAsync(Guid tenantId, SlaProfileFilter filter, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var query = db.SlaProfiles.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (filter.Search is { } search)
        {
            var value = search.ToUpperInvariant(); query = query.Where(p => p.Name.ToUpper().Contains(value));
        }
        if (filter.Status is { } status)
        {
            var value = Enum.Parse<SlaProfileStatus>(status, true); query = query.Where(p => p.Status == value);
        }
        var total = await query.LongCountAsync(cancellationToken);
        var profiles = await query.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip(checked((filter.Page - 1) * filter.PageSize))
            .Take(filter.PageSize).ToListAsync(cancellationToken);
        return new(profiles.Select(Row).ToArray(), filter.Page, filter.PageSize, total);
    }
    public async Task<SlaProfileRow> CreateAsync(SlaProfile profile, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(profile.TenantId);
        db.AddRange(profile, audit); await db.SaveChangesAsync(cancellationToken);
        return Row(profile);
    }
    private static SlaProfileRow Row(SlaProfile profile) => new(profile.Id, profile.Name, profile.Status.ToString().ToUpperInvariant());
}
