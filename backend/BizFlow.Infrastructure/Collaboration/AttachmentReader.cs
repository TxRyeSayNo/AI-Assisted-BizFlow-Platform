using BizFlow.Application.Collaboration;
using BizFlow.Domain.Collaboration;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class AttachmentReader(BizFlowDbContext db) : IAttachmentReader
{
    public async Task<IReadOnlyList<AttachmentItemView>> ListAttachmentsAsync(
        Guid tenantId,
        AttachmentObjectType objectType,
        Guid objectId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var rawRows = await db.Attachments.AsNoTracking()
            .Where(a => a.TenantId == tenantId &&
                        a.ObjectType == objectType &&
                        a.ObjectId == objectId &&
                        a.Status == AttachmentStatus.Ready &&
                        a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.ObjectType,
                a.ObjectId,
                a.UploadedBy,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.Status,
                a.Hash,
                a.CreatedAt,
                UploaderName = db.Users.Where(u => u.Id == a.UploadedBy).Select(u => u.FullName).FirstOrDefault(),
                UploaderEmail = db.Users.Where(u => u.Id == a.UploadedBy).Select(u => u.Email).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(r => new AttachmentItemView(
            r.Id,
            r.ObjectType.ToString().ToUpperInvariant(),
            r.ObjectId,
            r.UploadedBy,
            r.UploaderName ?? "User",
            r.UploaderEmail ?? string.Empty,
            r.FileName,
            r.ContentType,
            r.SizeBytes,
            r.Status.ToString().ToUpperInvariant(),
            r.Hash,
            r.CreatedAt,
            IsOwner: r.UploadedBy == currentUserId,
            CanDelete: r.UploadedBy == currentUserId || isAdmin
        )).ToArray();
    }

    public async Task<AttachmentItemView?> GetAttachmentViewAsync(
        Guid tenantId,
        Guid attachmentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var r = await db.Attachments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == attachmentId && a.DeletedAt == null)
            .Select(a => new
            {
                a.Id,
                a.ObjectType,
                a.ObjectId,
                a.UploadedBy,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.Status,
                a.Hash,
                a.CreatedAt,
                UploaderName = db.Users.Where(u => u.Id == a.UploadedBy).Select(u => u.FullName).FirstOrDefault(),
                UploaderEmail = db.Users.Where(u => u.Id == a.UploadedBy).Select(u => u.Email).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (r is null) return null;

        return new AttachmentItemView(
            r.Id,
            r.ObjectType.ToString().ToUpperInvariant(),
            r.ObjectId,
            r.UploadedBy,
            r.UploaderName ?? "User",
            r.UploaderEmail ?? string.Empty,
            r.FileName,
            r.ContentType,
            r.SizeBytes,
            r.Status.ToString().ToUpperInvariant(),
            r.Hash,
            r.CreatedAt,
            IsOwner: r.UploadedBy == currentUserId,
            CanDelete: r.UploadedBy == currentUserId || isAdmin
        );
    }

    public async Task<bool> TargetExistsAsync(
        Guid tenantId,
        AttachmentObjectType objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        return objectType switch
        {
            AttachmentObjectType.Task => await db.WorkTasks.AnyAsync(t => t.Id == objectId, cancellationToken),
            AttachmentObjectType.Request => await db.Requests.AnyAsync(r => r.Id == objectId, cancellationToken),
            AttachmentObjectType.Comment => await db.Comments.AnyAsync(c => c.Id == objectId, cancellationToken),
            AttachmentObjectType.Result => await db.TaskResults.AnyAsync(r => r.Id == objectId, cancellationToken),
            AttachmentObjectType.Progress => await db.TaskProgressReports.AnyAsync(p => p.Id == objectId, cancellationToken),
            _ => false
        };
    }
}
