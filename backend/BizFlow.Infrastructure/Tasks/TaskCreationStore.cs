using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Tasks;

public sealed class TaskCreationStore(BizFlowDbContext db, ITenantContext context) : ITaskCreationStore
{
    public async Task<ITaskCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
    {
        if (context.TenantId != tenantId || context.UserId != actorId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Task creation requires the current tenant and actor.");
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/TASK.CREATE/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            return new CreationTransaction(db, transaction, tenantId, actorId, keyHash);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private sealed class CreationTransaction(BizFlowDbContext db, IDbContextTransaction transaction,
        Guid tenantId, Guid actorId, string? keyHash) : ITaskCreationTransaction
    {
        public async Task<StoredTaskCreation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;
            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId}
                AND "Action"='TASK.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (audit is null) return null;
            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            using var after = JsonDocument.Parse(audit.AfterJson!);
            var root = after.RootElement;
            return new(metadata.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!,
                new(root.GetProperty("taskId").GetGuid(), root.GetProperty("title").GetString()!,
                    root.GetProperty("status").GetString()!, root.GetProperty("createdAt").GetDateTimeOffset()));
        }
        public async Task CommitAsync(WorkTask task, IReadOnlyList<TaskChecklistItem> checklist, AuditLog audit, CancellationToken cancellationToken)
        {
            if (task.TenantId != tenantId || task.CreatorId != actorId || audit.TenantId != tenantId ||
                audit.ActorId != actorId || audit.ObjectId != task.Id || audit.Action != "TASK.CREATED" || checklist.Any(c => c.TaskId != task.Id))
                throw new InvalidOperationException("Task, checklist and creation audit must share their transaction owner.");
            db.WorkTasks.Add(task); db.TaskChecklistItems.AddRange(checklist); db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
