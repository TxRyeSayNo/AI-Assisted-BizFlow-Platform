using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Tasks;

public sealed class TaskConfirmationStore(BizFlowDbContext db, ITenantContext context) : ITaskConfirmationStore
{
    public async Task<ITaskConfirmationTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty) throw ApplicationFault.NotFound();
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/TASK.RESULT_CONFIRM/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            var task = await db.WorkTasks.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Task" WHERE "TenantId"={tenantId} AND "TaskId"={taskId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            var assignment = task is null ? null : await db.TaskAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.TaskId == taskId && a.EndedAt == null, cancellationToken);
            var actor = task is null ? null : await db.Users.FromSqlInterpolated($"""
                SELECT *, xmin FROM "User" WHERE "TenantId"={tenantId} AND "UserId"={actorId} AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            return new ConfirmationTransaction(db, transaction, tenantId, actorId, keyHash, task, assignment, actor);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private sealed class ConfirmationTransaction(BizFlowDbContext db, IDbContextTransaction transaction, Guid tenantId, Guid actorId,
        string? keyHash, WorkTask? task, TaskAssignment? assignment, UserAccount? actor) : ITaskConfirmationTransaction
    {
        public WorkTask? Task => task;
        public TaskAssignment? Assignment => assignment;
        public UserAccount? Actor => actor;

        public async Task<StoredTaskConfirmation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action" IN ('TASK.RESULT_CONFIRMED', 'TASK.RESULT_REWORK')
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            return new(metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(root.GetProperty("taskId").GetGuid(), root.GetProperty("confirmationId").GetGuid(),
                    audit.Action == "TASK.RESULT_CONFIRMED" ? "CONFIRMED" : "REWORK",
                    root.GetProperty("status").GetString()!, root.GetProperty("confirmedAt").GetDateTimeOffset()));
        }

        public async Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken)
        {
            if (task is null || assignment is null || task.TenantId != tenantId ||
                audit.ActorId != actorId || audit.ObjectId != task.Id || audit.TenantId != tenantId ||
                (audit.Action != "TASK.RESULT_CONFIRMED" && audit.Action != "TASK.RESULT_REWORK"))
                throw new InvalidOperationException("Confirmation transaction ownership mismatch.");
            db.Confirmations.Add(confirmation);
            db.AuditLogs.Add(audit);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
