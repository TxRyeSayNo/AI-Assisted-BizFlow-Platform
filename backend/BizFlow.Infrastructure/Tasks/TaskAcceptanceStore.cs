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

public sealed class TaskAcceptanceStore(BizFlowDbContext db, ITenantContext context) : ITaskAcceptanceStore
{
    public async Task<ITaskAcceptanceTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty) throw ApplicationFault.NotFound();
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/TASK.ACCEPT/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            var task = await db.WorkTasks.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Task" WHERE "TenantId"={tenantId} AND "TaskId"={taskId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            var assignment = task is null ? null : await db.TaskAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.TaskId == taskId && a.EndedAt == null, cancellationToken);
            var actor = task is null ? null : await db.Users.FromSqlInterpolated($"""
                SELECT *, xmin FROM "User" WHERE "TenantId"={tenantId} AND "UserId"={actorId} AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            return new AcceptanceTransaction(db, transaction, tenantId, actorId, keyHash, task, assignment, actor);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private sealed class AcceptanceTransaction(BizFlowDbContext db, IDbContextTransaction transaction, Guid tenantId, Guid actorId,
        string? keyHash, WorkTask? task, TaskAssignment? assignment, UserAccount? actor) : ITaskAcceptanceTransaction
    {
        public WorkTask? Task => task;
        public TaskAssignment? Assignment => assignment;
        public UserAccount? Actor => actor;
        public async Task<StoredTaskAcceptance?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId} AND "Action"='TASK.ACCEPTED'
                AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!); using var after = JsonDocument.Parse(audit.AfterJson!); var root = after.RootElement;
            return new(metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(root.GetProperty("taskId").GetGuid(), root.GetProperty("assignmentId").GetGuid(), root.GetProperty("confirmationId").GetGuid(),
                    root.GetProperty("status").GetString()!, root.GetProperty("acceptedAt").GetDateTimeOffset()));
        }
        public async Task CommitAsync(Confirmation confirmation, AuditLog audit, Notification notification, CancellationToken cancellationToken)
        {
            if (task is null || assignment is null || assignment.UserId != actorId || task.TenantId != tenantId ||
                confirmation.ActorId != actorId || confirmation.ObjectId != task.Id || confirmation.TenantId != tenantId ||
                audit.ActorId != actorId || audit.ObjectId != task.Id || audit.TenantId != tenantId ||
                notification.TenantId != tenantId || notification.RecipientId != assignment.AssignedBy || notification.ObjectId != task.Id || notification.Type != "TaskAccepted")
                throw new InvalidOperationException("Acceptance transaction ownership mismatch.");
            // The receipt guard observes ASSIGNED before EF saves ACCEPTED. Existing-row EF
            // assignment edits remain disabled; only this authorized locked transaction updates it.
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "TaskAssignment" SET "UserId"={actorId},"AcceptedAt"={assignment.AcceptedAt}
                WHERE "TaskAssignmentId"={assignment.Id} AND "TaskId"={task.Id} AND "AcceptedAt" IS NULL AND "RejectedAt" IS NULL AND "EndedAt" IS NULL
                AND ("UserId"={actorId} OR ("UserId" IS NULL AND "DepartmentId"={actor!.DepartmentId}))
                """, cancellationToken);
            if (affected != 1) throw new ApplicationFault(FaultKind.Conflict, "TASK.ASSIGNMENT_CONFLICT", "The assignment changed before acceptance.");
            db.Confirmations.Add(confirmation); db.AuditLogs.Add(audit); db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
