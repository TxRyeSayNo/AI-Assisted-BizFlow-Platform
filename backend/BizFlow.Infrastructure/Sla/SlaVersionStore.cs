using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Sla;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Sla;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Sla;

public sealed class SlaVersionStore(BizFlowDbContext db, ITenantContext context) : ISlaVersionStore
{
    public async Task<ISlaVersionTransaction?> BeginAsync(Guid tenantId, Guid profileId, Guid calendarId, CancellationToken cancellationToken)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || tenantId == Guid.Empty || context.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA versions require a tenant workspace.");
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var profile = await db.SlaProfiles.FromSqlInterpolated($"""
                SELECT *, xmin FROM "SLAProfile" WHERE "SLAProfileId"={profileId} AND "TenantId"={tenantId} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (profile is null) { await transaction.DisposeAsync(); return null; }
            var calendar = await db.BusinessCalendars.FromSqlInterpolated($"""
                SELECT *, xmin FROM "BusinessCalendar" WHERE "CalendarId"={calendarId} AND "TenantId"={tenantId} FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (calendar is null) { await transaction.DisposeAsync(); return null; }
            var latest = await db.SlaVersions.Where(v => v.SlaProfileId == profileId).MaxAsync(v => (int?)v.VersionNo, cancellationToken) ?? 0;
            return new VersionTransaction(db, transaction, profile, calendar, latest);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private sealed class VersionTransaction(BizFlowDbContext db, IDbContextTransaction transaction,
        SlaProfile profile, BusinessCalendar calendar, int latest) : ISlaVersionTransaction
    {
        public SlaProfile Profile => profile;
        public BusinessCalendar Calendar => calendar;
        public int LatestVersionNo => latest;
        public async Task<bool> LockActiveRecipientsAsync(IReadOnlyList<Guid> recipients, CancellationToken cancellationToken)
        {
            if (recipients.Count == 0) return true;
            var ids = recipients.Distinct().Order().ToArray();
            var users = await db.Users.FromSqlInterpolated($"""
                SELECT *, xmin FROM "User" WHERE "UserId"=ANY({ids}) AND "TenantId"={profile.TenantId}
                ORDER BY "UserId" FOR SHARE
                """).AsNoTracking().ToListAsync(cancellationToken);
            return users.Count == ids.Length && users.All(u => u.Status == UserStatus.Active && u.DeletedAt is null);
        }
        public async Task CommitAsync(SlaVersion version, AuditLog audit, CancellationToken cancellationToken)
        {
            if (version.SlaProfileId != profile.Id || version.CalendarId != calendar.Id ||
                (long)version.VersionNo != (long)latest + 1 || audit.TenantId != profile.TenantId || audit.ObjectId != version.Id)
                throw new InvalidOperationException("SLA version and audit must belong to the locked configuration transaction.");
            db.AddRange(version, audit); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
