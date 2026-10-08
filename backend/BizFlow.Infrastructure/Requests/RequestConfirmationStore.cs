using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestConfirmationStore(BizFlowDbContext db, ITenantContext context) : IRequestConfirmationStore
{
    public async Task<IRequestConfirmationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid requestId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/REQUEST.CONFIRM/{keyHash}";
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

            return new ConfirmationTransaction(db, transaction, tenantId, actorId, keyHash, request, latestRouting, actor);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class ConfirmationTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        WorkRequest? request,
        RequestRouting? latestRouting,
        UserAccount? actor) : IRequestConfirmationTransaction
    {
        public WorkRequest? Request => request;
        public RequestRouting? LatestRouting => latestRouting;
        public UserAccount? Actor => actor;

        public async Task<StoredRequestConfirmation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action" IN ('REQUEST.CONFIRMED', 'REQUEST.REWORK')
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            var fingerprint = metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            var isConfirmed = audit.Action == "REQUEST.CONFIRMED";
            var decision = isConfirmed ? "CONFIRMED" : "REWORK";
            var status = isConfirmed ? "CLOSED" : "IN_PROGRESS";
            var confirmedAt = isConfirmed
                ? root.GetProperty("confirmedAt").GetDateTimeOffset()
                : root.GetProperty("requestedAt").GetDateTimeOffset();

            return new(fingerprint, new(
                root.GetProperty("requestId").GetGuid(),
                root.GetProperty("confirmationId").GetGuid(),
                decision,
                status,
                confirmedAt));
        }

        public async Task CommitAsync(
            Confirmation confirmation,
            AuditLog audit,
            Notification notification,
            Action? applyClosure,
            Func<AuditLog>? closeAuditFactory,
            CancellationToken cancellationToken)
        {
            if (applyClosure is not null && closeAuditFactory is not null)
            {
                // Confirmation case:
                // Step 1: Save Confirmation milestone record and REQUEST.CONFIRMED audit log while Request is Confirmed.
                db.Confirmations.Add(confirmation);
                db.AuditLogs.Add(audit);
                await db.SaveChangesAsync(cancellationToken);

                // Step 2: Apply closure mutation (Status = Closed in memory after Confirmed was flushed to DB),
                // create close audit log with valid Closed state, add in-app notification, and save.
                applyClosure();
                var closeAudit = closeAuditFactory();
                db.AuditLogs.Add(closeAudit);
                db.Notifications.Add(notification);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Rework case:
                // Single step: Save Confirmation rejection record, REQUEST.REWORK audit, and notification while Request is InProgress.
                db.Confirmations.Add(confirmation);
                db.AuditLogs.Add(audit);
                db.Notifications.Add(notification);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
