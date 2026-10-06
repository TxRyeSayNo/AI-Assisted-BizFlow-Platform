using System.Data;
using BizFlow.Application.Authentication;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Authentication;

public sealed class AuthenticationStore(DbContextOptions<BizFlowDbContext> options, TimeProvider clock) : IAuthenticationStore
{
    private BizFlowDbContext Create(Guid? userId = null, Guid? tenantId = null) => new(options, new AuthenticationContext(userId, tenantId));

    public async Task<IReadOnlyList<AuthenticationCandidate>> FindCandidatesAsync(string normalizedIdentifier, string? tenantKey, CancellationToken cancellationToken)
    {
        await using var db = Create();
        // Only identity resolution bypasses default filters. No business objects or password hashes leave this query.
        var query = from user in db.Users.IgnoreQueryFilters().AsNoTracking()
                    join tenant in db.Tenants.IgnoreQueryFilters() on user.TenantId equals tenant.Id into tenants
                    from tenant in tenants.DefaultIfEmpty()
                    where user.DeletedAt == null && user.Status == UserStatus.Active &&
                        (user.NormalizedEmployeeCode == normalizedIdentifier || user.NormalizedEmail == normalizedIdentifier) &&
                        (tenantKey == null || (tenant != null && tenant.TenantKey == tenantKey))
                    orderby user.Id
                    select new AuthenticationCandidate(user.Id, user.TenantId, tenant == null ? null : tenant.Name);
        // Bound anonymous lookup results; more than one always requires workspace context.
        return await query.Take(20).ToListAsync(cancellationToken);
    }

    public async Task<AuthenticationCandidate?> FindSessionOwnerAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var db = Create();
        return await (from session in db.AuthenticationSessions.IgnoreQueryFilters()
                      join user in db.Users.IgnoreQueryFilters() on session.UserId equals user.Id
                      where session.TokenHash == tokenHash
                      select new AuthenticationCandidate(user.Id, user.TenantId, null)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IAuthenticationTransaction?> BeginAsync(AuthenticationCandidate candidate, CancellationToken cancellationToken)
    {
        var db = Create(candidate.UserId, candidate.TenantId);
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // A shared per-user lock order serializes login, refresh/replay and future password resets.
            // Include xmin explicitly because EF maps the PostgreSQL system column as Version.
            var users = await db.Users.FromSqlInterpolated($"SELECT *, xmin FROM \"User\" WHERE \"UserId\" = {candidate.UserId} FOR UPDATE")
                .IgnoreQueryFilters().ToListAsync(cancellationToken);
            var user = users.SingleOrDefault();
            if (user is null || user.TenantId != candidate.TenantId)
            {
                await transaction.DisposeAsync(); await db.DisposeAsync(); return null;
            }
            var name = user.TenantId is { } tenantId ? await db.Tenants.IgnoreQueryFilters()
                .Where(t => t.Id == tenantId).Select(t => t.Name).SingleAsync(cancellationToken) : null;
            return new AuthenticationTransaction(db, transaction, user, name, clock);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await db.DisposeAsync(); throw;
        }
    }

    public async Task<bool> ValidateAccessAsync(AccessTokenRequest identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = Create(identity.UserId, identity.TenantId);
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Id == identity.UserId &&
            u.TenantId == identity.TenantId && u.SecurityStamp == identity.SecurityStamp, cancellationToken);
        if (user is null || !user.CanAuthenticate(now)) return false;
        var access = await new AccessSnapshotProvider(db, clock).ResolveAsync(user.Id, user.TenantId, cancellationToken);
        return access is { IsUserActive: true, IsTenantActive: true } && await db.AuthenticationSessions.AnyAsync(s =>
            s.UserId == identity.UserId && s.FamilyId == identity.FamilyId && s.RevokedAt == null && s.CreatedAt <= now && s.ExpiresAt > now, cancellationToken);
    }

    private sealed record AuthenticationContext(Guid? UserId, Guid? TenantId) : ITenantContext;

    private sealed class AuthenticationTransaction(BizFlowDbContext db, IDbContextTransaction transaction,
        UserAccount user, string? tenantName, TimeProvider clock) : IAuthenticationTransaction
    {
        public UserAccount User => user;
        public string? TenantName => tenantName;
        public Task<AccessSnapshot?> ResolveAccessAsync(CancellationToken cancellationToken) =>
            new AccessSnapshotProvider(db, clock).ResolveAsync(user.Id, user.TenantId, cancellationToken);
        public Task<AuthenticationSession?> FindSessionAsync(string hash, CancellationToken cancellationToken) =>
            db.AuthenticationSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var sessions = await db.AuthenticationSessions.Where(s => s.FamilyId == familyId && s.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var session in sessions) session.Revoke(now);
        }
        public void AddSession(AuthenticationSession session) => db.AuthenticationSessions.Add(session);
        public async Task RevokeAllSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var sessions = await db.AuthenticationSessions.Where(s => s.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var session in sessions) session.Revoke(now);
        }
        public void Audit(AuthenticationEvent action, DateTimeOffset now, Guid? familyId = null) =>
            db.AuditLogs.Add(AuditLog.Authentication(user.TenantId, user.Id, action, now, familyId));
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync(); await db.DisposeAsync();
        }
    }
}
