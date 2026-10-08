using System.Text.Json;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class CommentStore(BizFlowDbContext db, ITenantContext context) : ICommentStore
{
    public async Task<ICommentCreationTransaction> BeginCreateAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
    {
        if (context.TenantId != tenantId || context.UserId != actorId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Commenting requires the current tenant and actor.");

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/COMMENT.CREATE/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            return new CommentCreationTransaction(db, transaction, tenantId, actorId, keyHash);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<Comment?> FindCommentAsync(Guid tenantId, Guid commentId, CancellationToken cancellationToken)
    {
        return await db.Comments.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == commentId && c.DeletedAt == null, cancellationToken);
    }

    public async Task UpdateCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.UserRoles.AnyAsync(ur => ur.UserId == userId &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "COMPANY_ADMIN"), cancellationToken);
    }

    private sealed class CommentCreationTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash) : ICommentCreationTransaction
    {
        public async Task<StoredCommentCreation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;

            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId}
                AND "Action"='COMMENT.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadataDoc = JsonDocument.Parse(audit.MetadataJson!);
            using var afterDoc = JsonDocument.Parse(audit.AfterJson!);
            var root = afterDoc.RootElement;

            var authorId = root.GetProperty("authorId").GetGuid();
            var author = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == authorId, cancellationToken);

            var commentId = root.GetProperty("commentId").GetGuid();
            var objectType = root.GetProperty("objectType").GetString()!;
            var objectId = root.GetProperty("objectId").GetGuid();
            var content = root.GetProperty("content").GetString()!;
            var createdAt = root.GetProperty("createdAt").GetDateTimeOffset();

            var fingerprint = metadataDoc.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;
            var view = new CommentItemView(
                commentId,
                objectType,
                objectId,
                authorId,
                author?.FullName ?? "User",
                author?.Email ?? string.Empty,
                content,
                createdAt,
                EditedAt: null,
                IsOwner: true,
                CanDelete: true);

            return new StoredCommentCreation(fingerprint, view);
        }

        public async Task<bool> ValidateTargetExistsAsync(CommentObjectType objectType, Guid objectId, CancellationToken cancellationToken)
        {
            return objectType switch
            {
                CommentObjectType.Task => await db.WorkTasks.AsNoTracking().AnyAsync(t => t.TenantId == tenantId && t.Id == objectId && t.DeletedAt == null, cancellationToken),
                CommentObjectType.Request => await db.Requests.AsNoTracking().AnyAsync(r => r.TenantId == tenantId && r.Id == objectId && r.DeletedAt == null, cancellationToken),
                CommentObjectType.Result => await db.TaskResults.AsNoTracking().AnyAsync(r => r.Id == objectId, cancellationToken),
                CommentObjectType.Progress => await db.TaskProgressReports.AsNoTracking().AnyAsync(p => p.Id == objectId, cancellationToken),
                _ => false
            };
        }

        public async Task CommitAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken)
        {
            if (comment.TenantId != tenantId || comment.AuthorId != actorId ||
                audit.TenantId != tenantId || audit.ActorId != actorId || audit.ObjectId != comment.Id ||
                audit.Action != "COMMENT.CREATED")
            {
                throw new InvalidOperationException("Comment and creation audit must match their transaction context.");
            }

            db.Comments.Add(comment);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
