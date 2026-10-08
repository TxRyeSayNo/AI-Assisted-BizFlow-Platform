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

public sealed class AttachmentStore(BizFlowDbContext db, ITenantContext context) : IAttachmentStore
{
    public async Task<IAttachmentCreationTransaction> BeginCreateSessionAsync(
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        CancellationToken cancellationToken)
    {
        if (context.TenantId != tenantId || context.UserId != actorId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Attachments require the current tenant and actor.");

        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (keyHash is not null)
            {
                var lockKey = $"{tenantId:N}/{actorId:N}/ATTACHMENT.CREATE_UPLOAD_SESSION/{keyHash}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }
            return new AttachmentCreationTransaction(db, transaction, tenantId, actorId, keyHash);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<Attachment?> FindAttachmentAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken)
    {
        return await db.Attachments.SingleOrDefaultAsync(
            a => a.TenantId == tenantId && a.Id == attachmentId && a.DeletedAt == null,
            cancellationToken);
    }

    public async Task FinalizeAttachmentAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAttachmentAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        return await db.UserRoles.AnyAsync(ur => ur.UserId == userId &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "COMPANY_ADMIN"), cancellationToken);
    }

    public async Task<long?> GetTenantMaxUploadSizeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var setting = await db.TenantSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "attachment.max_size_bytes", cancellationToken);

        if (setting is not null && long.TryParse(setting.ValueJson, out var max) && max > 0 && max <= Attachment.MaxFileSizeBytes)
        {
            return max;
        }

        return null;
    }

    public async Task<IReadOnlyList<string>?> GetTenantAllowedContentTypesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var setting = await db.TenantSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "attachment.allowed_content_types", cancellationToken);

        if (setting is null || string.IsNullOrWhiteSpace(setting.ValueJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(setting.ValueJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                    {
                        list.Add(el.GetString()!.Trim().ToLowerInvariant());
                    }
                }
                return list.Count > 0 ? list : null;
            }
        }
        catch
        {
            // fallback to default
        }

        return null;
    }

    private sealed class AttachmentCreationTransaction(
        BizFlowDbContext db,
        IDbContextTransaction transaction,
        Guid tenantId,
        Guid actorId,
        string? keyHash) : IAttachmentCreationTransaction
    {
        public async Task<StoredUploadSessionCreation?> FindReplayAsync(CancellationToken cancellationToken)
        {
            if (keyHash is null) return null;

            var audit = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM "AuditLog" WHERE "TenantId"={tenantId} AND "ActorId"={actorId}
                AND "Action"='ATTACHMENT.UPLOAD_SESSION_CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash'={keyHash}
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (audit is null) return null;

            using var metadataDoc = JsonDocument.Parse(audit.MetadataJson!);
            using var afterDoc = JsonDocument.Parse(audit.AfterJson!);
            var root = afterDoc.RootElement;

            var attachmentId = root.GetProperty("attachmentId").GetGuid();
            var objectKey = root.GetProperty("objectKey").GetString()!;
            var fingerprint = metadataDoc.RootElement.GetProperty("idempotency").GetProperty("fingerprint").GetString()!;

            var result = new UploadSessionResult(
                attachmentId,
                objectKey,
                $"/api/v1/attachments/upload-session/binary?key={Uri.EscapeDataString(objectKey)}",
                DateTimeOffset.UtcNow.AddHours(1));

            return new StoredUploadSessionCreation(fingerprint, result);
        }

        public async Task<bool> ValidateTargetExistsAsync(AttachmentObjectType objectType, Guid objectId, CancellationToken cancellationToken)
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

        public async Task CommitAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
        {
            db.Attachments.Add(attachment);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
