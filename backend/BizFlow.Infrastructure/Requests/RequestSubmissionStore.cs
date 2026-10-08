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

public sealed class RequestSubmissionStore(BizFlowDbContext db, ITenantContext context) : IRequestSubmissionStore
{
    public async Task<IRequestSubmissionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        db.Database.AutoSavepointsEnabled = false;

        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.SUBMIT/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }

            var request = await db.Requests.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Request" WHERE "TenantId"={tenantId} AND "RequestId"={requestId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);

            return new SubmissionTransaction(db, transaction, tenantId, actorId, keyHash, request);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class SubmissionTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request) : IRequestSubmissionTransaction
    {
        public WorkRequest? Request => request;

        public async Task<StoredRequestSubmission?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.SUBMITTED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;

            return new(
                metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(
                    root.GetProperty("requestId").GetGuid(),
                    root.GetProperty("status").GetString()!,
                    root.GetProperty("submittedAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(AuditLog audit, Notification notification, CancellationToken cancellationToken)
        {
            if (request is null || request.RequesterId != actorId || request.TenantId != tenantId ||
                audit.ActorId != actorId || audit.ObjectId != request.Id || audit.TenantId != tenantId ||
                audit.Action != "REQUEST.SUBMITTED")
                throw new InvalidOperationException("Submission transaction ownership mismatch.");

            db.AuditLogs.Add(audit);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
