using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestReviseStore(BizFlowDbContext db, ITenantContext context) : IRequestReviseStore
{
    public async Task<IRequestReviseTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid sourceRequestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.REVISE/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }

            var source = await db.Requests.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Request" WHERE "TenantId"={tenantId} AND "RequestId"={sourceRequestId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

            var actor = await db.Users
                .SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == actorId && u.DeletedAt == null, cancellationToken);

            return new ReviseTransaction(db, transaction, tenantId, actorId, keyHash, source, actor);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class ReviseTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? sourceRequest,
        UserAccount? actor) : IRequestReviseTransaction
    {
        public WorkRequest? SourceRequest => sourceRequest;
        public UserAccount? Actor => actor;

        public async Task<StoredRequestRevision?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.REVISED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("sourceRequestId").GetGuid(),
                root.GetProperty("title").GetString()!,
                root.GetProperty("status").GetString()!,
                root.GetProperty("createdAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(WorkRequest revision, AuditLog audit, CancellationToken cancellationToken)
        {
            db.Requests.Add(revision);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
