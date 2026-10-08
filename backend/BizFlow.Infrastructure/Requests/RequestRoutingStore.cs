using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestRoutingStore(BizFlowDbContext db, ITenantContext context) : IRequestRoutingStore
{
    public async Task<IRequestRoutingTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.ROUTE/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }

            var request = await db.Requests.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Request" WHERE "TenantId"={tenantId} AND "RequestId"={requestId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

            var latestRouting = request is null
                ? null
                : await db.RequestRoutings
                    .Where(r => r.TenantId == tenantId && r.RequestId == requestId)
                    .OrderByDescending(r => r.RoutedAt)
                    .FirstOrDefaultAsync(cancellationToken);

            return new RoutingTransaction(db, transaction, tenantId, actorId, keyHash, request, latestRouting);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class RoutingTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request,
        RequestRouting? latestRouting) : IRequestRoutingTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => latestRouting;

        public async Task<bool> DepartmentExistsAndActiveAsync(Guid departmentId, CancellationToken cancellationToken) =>
            await db.Departments.AnyAsync(d => d.TenantId == tenantId && d.Id == departmentId && d.Status == BizFlow.Domain.Organization.RecordStatus.Active, cancellationToken);

        public async Task<bool> UserExistsAndActiveAsync(Guid userId, CancellationToken cancellationToken) =>
            await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == userId && u.Status == BizFlow.Domain.Organization.UserStatus.Active && u.DeletedAt == null, cancellationToken);

        public async Task<IReadOnlyList<Guid>> GetDepartmentManagersAsync(Guid departmentId, CancellationToken cancellationToken) =>
            await db.ManagementScopes
                .Where(m => m.TenantId == tenantId && m.DepartmentId == departmentId)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

        public async Task<StoredRequestRouting?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.ROUTED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            Guid? toDeptId = root.TryGetProperty("toDepartmentId", out var dp) && dp.ValueKind != JsonValueKind.Null ? dp.GetGuid() : null;
            Guid? toUsrId = root.TryGetProperty("toUserId", out var up) && up.ValueKind != JsonValueKind.Null ? up.GetGuid() : null;

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("routingId").GetGuid(),
                root.GetProperty("status").GetString()!,
                toDeptId,
                toUsrId,
                root.GetProperty("routedAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(RequestRouting routing, AuditLog audit, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
        {
            if (request is null || request.TenantId != tenantId ||
                routing.TenantId != tenantId || routing.RequestId != request.Id ||
                audit.TenantId != tenantId || audit.ActorId != actorId || audit.ObjectId != request.Id)
                throw new InvalidOperationException("Routing transaction ownership mismatch.");

            db.RequestRoutings.Add(routing);
            db.AuditLogs.Add(audit);
            if (notifications.Count > 0)
            {
                db.Notifications.AddRange(notifications);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
