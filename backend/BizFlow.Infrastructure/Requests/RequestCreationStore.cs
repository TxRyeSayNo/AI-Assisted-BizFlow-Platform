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

public sealed class RequestCreationStore(BizFlowDbContext db, ITenantContext context) : IRequestCreationStore
{
    public async Task<IRequestCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
    {
        if (context.TenantId != tenantId || context.UserId != actorId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Request creation requires the current tenant and actor.");

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.CREATE/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            return new CreationTransaction(db, transaction, tenantId, actorId, keyHash);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class CreationTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash) : IRequestCreationTransaction
    {
        public async Task<StoredRequestCreation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId}
                AND "Action"='REQUEST.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;

            return new(
                metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(
                    root.GetProperty("requestId").GetGuid(),
                    root.GetProperty("title").GetString()!,
                    root.GetProperty("status").GetString()!,
                    root.GetProperty("priority").GetString()!,
                    root.GetProperty("serviceId").GetGuid(),
                    root.GetProperty("categoryId").GetGuid(),
                    root.GetProperty("createdAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(WorkRequest request, AuditLog audit, CancellationToken cancellationToken)
        {
            if (request.TenantId != tenantId || request.RequesterId != actorId || audit.TenantId != tenantId ||
                audit.ActorId != actorId || audit.ObjectId != request.Id || audit.Action != "REQUEST.CREATED")
                throw new InvalidOperationException("Request and creation audit must share their transaction owner.");

            db.Requests.Add(request);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
