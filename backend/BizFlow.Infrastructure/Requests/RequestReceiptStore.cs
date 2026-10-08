using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestReceiptStore(BizFlowDbContext db, ITenantContext context) : IRequestReceiptStore
{
    public async Task<IRequestReceiptTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.RECEIVE/{keyHash}";
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

            return new ReceiptTransaction(db, transaction, tenantId, actorId, keyHash, request, latestRouting);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class ReceiptTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request,
        RequestRouting? latestRouting) : IRequestReceiptTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => latestRouting;

        public async Task<bool> IsUserInDepartmentAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken) =>
            await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == userId && u.DepartmentId == departmentId && u.Status == BizFlow.Domain.Organization.UserStatus.Active && u.DeletedAt == null, cancellationToken);

        public async Task<StoredRequestReceipt?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='REQUEST.RECEIVED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("confirmationId").GetGuid(),
                root.GetProperty("status").GetString()!,
                root.GetProperty("receivedAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken)
        {
            if (request is null || request.TenantId != tenantId ||
                confirmation.TenantId != tenantId || confirmation.ObjectId != request.Id ||
                audit.TenantId != tenantId || audit.ActorId != actorId || audit.ObjectId != request.Id)
                throw new InvalidOperationException("Receipt transaction ownership mismatch.");

            db.Confirmations.Add(confirmation);
            db.AuditLogs.Add(audit);
            db.Notifications.Add(notification);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
