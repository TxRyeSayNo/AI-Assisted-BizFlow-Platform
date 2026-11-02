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

public sealed class TaskAssignmentStore(BizFlowDbContext db, ITenantContext context) : ITaskAssignmentStore
{
    public async Task<ITaskAssignmentTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId || actorId != context.UserId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw ApplicationFault.NotFound();
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/TASK.ASSIGN/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            var task = await db.WorkTasks.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Task" WHERE "TenantId"={tenantId} AND "TaskId"={taskId} AND "DeletedAt" IS NULL FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            var current = task is null ? null : await db.TaskAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.TaskId == taskId && a.EndedAt == null, cancellationToken);
            return new AssignmentTransaction(db, transaction, tenantId, actorId, keyHash, task, current);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class AssignmentTransaction(BizFlowDbContext db, IDbContextTransaction transaction, Guid tenantId,
        Guid actorId, string? keyHash, WorkTask? task, TaskAssignment? current) : ITaskAssignmentTransaction
    {
        public WorkTask? Task => task;
        public TaskAssignment? CurrentAssignment => current;
        public async Task<AssignmentTarget?> LoadTargetAsync(string type, Guid id, bool requireActive, CancellationToken cancellationToken)
        {
            if (type == "USER")
            {
                var user = await db.Users.FromSqlInterpolated($"""
                    SELECT *, xmin FROM "User" WHERE "TenantId"={tenantId} AND "UserId"={id} AND (NOT {requireActive} OR "Status"='ACTIVE') AND "DeletedAt" IS NULL FOR SHARE
                    """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (user is null) return null;
                var isManager = await (from ur in db.UserRoles
                                       join r in db.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == id && r.Name == "MANAGER"
                                       select ur).AnyAsync(cancellationToken);
                return new(id, null, user.DepartmentId ?? Guid.Empty, [id], isManager);
            }
            var target = await db.Departments.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Department" WHERE "TenantId"={tenantId} AND "DepartmentId"={id} AND (NOT {requireActive} OR "Status"='ACTIVE') FOR SHARE
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (target is null) return null;
            if (!requireActive) return new(null, id, id, [], true);
            // In accordance with Requirement A & G: Do NOT broadcast to all employees in the department!
            // Instead, resolve the responsible Manager(s) of that department.
            var managerUsers = await (from u in db.Users
                                      join ur in db.UserRoles on u.Id equals ur.UserId
                                      join r in db.Roles on ur.RoleId equals r.Id
                                      where u.TenantId == tenantId && u.DepartmentId == id
                                        && (!requireActive || u.Status == UserStatus.Active) && u.DeletedAt == null
                                        && r.Name == "MANAGER"
                                      select u.Id).Distinct().ToArrayAsync(cancellationToken);
            if (managerUsers.Length == 0)
            {
                // Fallback to checking ManagementScope
                managerUsers = await (from ms in db.ManagementScopes
                                      join u in db.Users on ms.UserId equals u.Id
                                      where ms.TenantId == tenantId && ms.DepartmentId == id
                                        && (!requireActive || u.Status == UserStatus.Active) && u.DeletedAt == null
                                      select u.Id).Distinct().ToArrayAsync(cancellationToken);
            }
            var recipients = managerUsers.Length > 0 ? managerUsers : [];
            return new(null, id, id, recipients, true);
        }
        public async Task<StoredTaskAssignment?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId}
                AND "Action"='TASK.ASSIGNED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!); using var after = JsonDocument.Parse(audit.AfterJson!); var root = after.RootElement;
            return new(metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(root.GetProperty("taskId").GetGuid(), root.GetProperty("assignmentId").GetGuid(), root.GetProperty("status").GetString()!, root.GetProperty("assignedAt").GetDateTimeOffset()));
        }
        public async Task CommitAsync(TaskAssignment assignment, AuditLog audit, IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
        {
            if (task is null || task.TenantId != tenantId || assignment.TaskId != task.Id || assignment.AssignedBy != actorId ||
                audit.ObjectId != task.Id || audit.ActorId != actorId || audit.TenantId != tenantId ||
                notifications.Any(n => n.TenantId != tenantId || n.ObjectId != task.Id || n.Type != "TaskAssigned")) throw new InvalidOperationException("Assignment transaction ownership mismatch.");
            if (current is not null)
            {
                var ended = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "TaskAssignment" SET "EndedAt"={assignment.AssignedAt} WHERE "TaskAssignmentId"={current.Id}
                    AND "TaskId"={task.Id} AND "EndedAt" IS NULL AND "RejectedAt" IS NOT NULL AND "AcceptedAt" IS NULL
                    """, cancellationToken);
                if (ended != 1) throw new ApplicationFault(FaultKind.Conflict, "TASK.ASSIGNMENT_CONFLICT", "The previous assignment changed.");
            }
            db.TaskAssignments.Add(assignment); db.AuditLogs.Add(audit); db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
