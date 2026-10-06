using System.Data;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Sla;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Sla;

public sealed class SlaVersionHistoryStore(BizFlowDbContext db, ITenantContext context) : ISlaVersionHistoryStore
{
    public async Task<SlaVersionHistoryPage?> ReadAsync(Guid tenantId, Guid profileId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || context.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA history requires a tenant workspace.");
        // One consistent view of metadata, count and page; all three entity sets retain tenant filters.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var profile = await db.SlaProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == profileId && p.TenantId == tenantId, cancellationToken);
        if (profile is null) return null;
        var versions = db.SlaVersions.AsNoTracking().Where(v => v.SlaProfileId == profileId);
        var total = await versions.LongCountAsync(cancellationToken);
        var rows = await (from version in versions
                          join calendar in db.BusinessCalendars.AsNoTracking() on version.CalendarId equals calendar.Id
                          where calendar.TenantId == tenantId
                          orderby version.VersionNo descending
                          select new { Version = version, Calendar = calendar })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = rows.Select(r => new SlaVersionHistoryRow(r.Version.Id, r.Version.VersionNo, r.Version.TargetMinutes,
            r.Version.WarningMinutes, r.Version.EscalationConfigJson is null ? null : JsonSerializer.Deserialize<JsonElement>(r.Version.EscalationConfigJson),
            new(r.Calendar.Id, r.Calendar.TimeZone, JsonSerializer.Deserialize<JsonElement>(r.Calendar.WorkingHoursJson),
                JsonSerializer.Deserialize<JsonElement>(r.Calendar.HolidaysJson)))).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(profile.Id, profile.Name, items, page, pageSize, total);
    }
}
