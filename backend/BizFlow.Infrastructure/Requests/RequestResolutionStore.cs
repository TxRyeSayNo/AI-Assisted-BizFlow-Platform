using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestResolutionStore(BizFlowDbContext db, ITenantContext context) : IRequestResolutionStore
{
    public async Task<IRequestResolutionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.RESOLVE/{keyHash}";
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

            var actor = await db.Users
                .SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == actorId && u.DeletedAt == null, cancellationToken);

            return new ResolutionTransaction(db, transaction, tenantId, actorId, keyHash, request, latestRouting, actor);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class ResolutionTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request,
        RequestRouting? latestRouting,
        UserAccount? actor) : IRequestResolutionTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => latestRouting;
        public UserAccount? Actor => actor;

        public async Task<int> NextRevisionNoAsync(CancellationToken cancellationToken)
        {
            if (request is null) return 1;
            var max = await db.RequestResolutions
                .Where(r => r.RequestId == request.Id)
                .MaxAsync(r => (int?)r.RevisionNo, cancellationToken);
            return (max ?? 0) + 1;
        }

        public async Task<StoredRequestResolution?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.RESOLVED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("resolutionId").GetGuid(),
                root.GetProperty("revisionNo").GetInt32(),
                root.GetProperty("content").GetString()!,
                root.GetProperty("status").GetString()!,
                root.GetProperty("resolvedAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(
            RequestResolution resolution,
            Action applyTransition,
            Func<AuditLog> auditFactory,
            Notification notification,
            CancellationToken cancellationToken)
        {
            // Step 1: Insert RequestResolution first while Request row in DB and ChangeTracker is still IN_PROGRESS.
            // This satisfies PostgreSQL's bizflow_request_resolution_guard() trigger.
            db.RequestResolutions.Add(resolution);
            await db.SaveChangesAsync(cancellationToken);

            // Step 2: Transition Request to RESOLVED, generate AuditLog with valid post-transition state, and commit.
            applyTransition();
            var audit = auditFactory();
            db.AuditLogs.Add(audit);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);

            // Step 3: Complete atomic database transaction.
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
