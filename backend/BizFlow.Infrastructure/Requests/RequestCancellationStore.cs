using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestCancellationStore(BizFlowDbContext db, ITenantContext context) : IRequestCancellationStore
{
    public async Task<IRequestCancellationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.CANCEL/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }

            var request = await db.Requests.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Request" WHERE "TenantId"={tenantId} AND "RequestId"={requestId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

            return new CancellationTransaction(db, transaction, tenantId, actorId, keyHash, request);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class CancellationTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request) : IRequestCancellationTransaction
    {
        public WorkRequest? Request => request;

        public async Task<StoredRequestCancellation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.CANCELLED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("status").GetString()!,
                root.GetProperty("cancelledAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(AuditLog audit, CancellationToken cancellationToken)
        {
            if (request is null || request.TenantId != tenantId ||
                audit.TenantId != tenantId || audit.ActorId != actorId || audit.ObjectId != request.Id)
                throw new InvalidOperationException("Cancellation transaction ownership mismatch.");

            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
