using BizFlow.Application.Collaboration;
using BizFlow.Domain.Collaboration;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class CommentReader(BizFlowDbContext db) : ICommentReader
{
    public async Task<IReadOnlyList<CommentItemView>> ListCommentsAsync(
        Guid tenantId,
        CommentObjectType objectType,
        Guid objectId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var rawRows = await db.Comments.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ObjectType == objectType && c.ObjectId == objectId && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ObjectType,
                c.ObjectId,
                c.AuthorId,
                c.Content,
                c.CreatedAt,
                c.EditedAt,
                AuthorName = db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName).FirstOrDefault(),
                AuthorEmail = db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.Email).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(r => new CommentItemView(
            r.Id,
            r.ObjectType.ToString().ToUpperInvariant(),
            r.ObjectId,
            r.AuthorId,
            r.AuthorName ?? "User",
            r.AuthorEmail ?? string.Empty,
            r.Content,
            r.CreatedAt,
            r.EditedAt,
            IsOwner: r.AuthorId == currentUserId,
            CanDelete: r.AuthorId == currentUserId || isAdmin
        )).ToArray();
    }

    public async Task<CommentItemView?> GetCommentByIdAsync(
        Guid tenantId,
        Guid commentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var r = await db.Comments.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == commentId && c.DeletedAt == null)
            .Select(c => new
            {
                c.Id,
                c.ObjectType,
                c.ObjectId,
                c.AuthorId,
                c.Content,
                c.CreatedAt,
                c.EditedAt,
                AuthorName = db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName).FirstOrDefault(),
                AuthorEmail = db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.Email).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (r is null) return null;

        return new CommentItemView(
            r.Id,
            r.ObjectType.ToString().ToUpperInvariant(),
            r.ObjectId,
            r.AuthorId,
            r.AuthorName ?? "User",
            r.AuthorEmail ?? string.Empty,
            r.Content,
            r.CreatedAt,
            r.EditedAt,
            IsOwner: r.AuthorId == currentUserId,
            CanDelete: r.AuthorId == currentUserId || isAdmin
        );
    }
}
