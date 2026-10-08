using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;

namespace BizFlow.Application.Collaboration;

public sealed record AttachmentItemView(
    Guid AttachmentId,
    string ObjectType,
    Guid ObjectId,
    Guid UploadedBy,
    string UploaderName,
    string UploaderEmail,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    string Hash,
    DateTimeOffset CreatedAt,
    bool IsOwner,
    bool CanDelete);

public sealed record CreateUploadSessionCommand(
    string ObjectType,
    Guid ObjectId,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record UploadSessionResult(
    Guid AttachmentId,
    string ObjectKey,
    string UploadUrl,
    DateTimeOffset ExpiresAt);

public sealed record FinalizeAttachmentCommand(
    Guid AttachmentId,
    string? ClientHash);

public sealed record AttachmentDownloadDescriptor(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record StoredUploadSessionCreation(
    string Fingerprint,
    UploadSessionResult Result);

public sealed record UploadSessionDescriptor(
    string UploadUrl,
    DateTimeOffset ExpiresAt);

public sealed record StorageFileInspection(
    bool Exists,
    long SizeBytes,
    string HashSha256);

public interface IObjectStorageProvider
{
    Task<UploadSessionDescriptor> CreateUploadSessionAsync(
        Guid attachmentId,
        string objectKey,
        string contentType,
        long sizeBytes,
        TimeSpan expiry,
        CancellationToken cancellationToken);

    Task SaveBinaryDirectAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken);

    Task<StorageFileInspection> InspectFileAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task<bool> DeleteFileAsync(
        string objectKey,
        CancellationToken cancellationToken);
}

public interface IAttachmentCreationTransaction : IAsyncDisposable
{
    Task<StoredUploadSessionCreation?> FindReplayAsync(CancellationToken cancellationToken);
    Task<bool> ValidateTargetExistsAsync(AttachmentObjectType objectType, Guid objectId, CancellationToken cancellationToken);
    Task CommitAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken);
}

public interface IAttachmentStore
{
    Task<IAttachmentCreationTransaction> BeginCreateSessionAsync(
        Guid tenantId,
        Guid actorId,
        string? keyHash,
        CancellationToken cancellationToken);

    Task<Attachment?> FindAttachmentAsync(
        Guid tenantId,
        Guid attachmentId,
        CancellationToken cancellationToken);

    Task FinalizeAttachmentAsync(
        Attachment attachment,
        AuditLog audit,
        CancellationToken cancellationToken);

    Task DeleteAttachmentAsync(
        Attachment attachment,
        AuditLog audit,
        CancellationToken cancellationToken);

    Task<bool> IsUserAdminAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<long?> GetTenantMaxUploadSizeAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>?> GetTenantAllowedContentTypesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public interface IAttachmentReader
{
    Task<IReadOnlyList<AttachmentItemView>> ListAttachmentsAsync(
        Guid tenantId,
        AttachmentObjectType objectType,
        Guid objectId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<AttachmentItemView?> GetAttachmentViewAsync(
        Guid tenantId,
        Guid attachmentId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<bool> TargetExistsAsync(
        Guid tenantId,
        AttachmentObjectType objectType,
        Guid objectId,
        CancellationToken cancellationToken);
}

public static class AttachmentRules
{
    public static readonly string[] DefaultAllowedContentTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/zip",
        "application/x-zip-compressed"
    ];

    public static AttachmentObjectType ParseObjectType(string objectType)
    {
        if (string.IsNullOrWhiteSpace(objectType))
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.OBJECT_TYPE_REQUIRED", "Target object type is required.");

        return objectType.Trim().ToUpperInvariant() switch
        {
            "TASK" => AttachmentObjectType.Task,
            "REQUEST" => AttachmentObjectType.Request,
            "COMMENT" => AttachmentObjectType.Comment,
            "RESULT" => AttachmentObjectType.Result,
            "PROGRESS" => AttachmentObjectType.Progress,
            _ => throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.INVALID_OBJECT_TYPE",
                "Object type must be TASK, REQUEST, COMMENT, RESULT, or PROGRESS.")
        };
    }

    public static bool IsContentTypeAllowed(string contentType, IReadOnlyList<string>? customAllowlist)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var normalized = contentType.Trim().ToLowerInvariant();
        var allowlist = (customAllowlist != null && customAllowlist.Count > 0)
            ? customAllowlist
            : DefaultAllowedContentTypes;

        return allowlist.Any(a => string.Equals(a.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.INVALID_FILE_NAME", "File name cannot be empty.");

        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.INVALID_FILE_NAME", "File name is invalid.");

        return name.Length > 255 ? name[..255] : name;
    }

    public static string BuildObjectKey(Guid tenantId, Guid attachmentId, string sanitizedFileName)
    {
        var now = DateTimeOffset.UtcNow;
        return $"tenants/{tenantId:D}/{now:yyyy}/{now:MM}/{attachmentId:D}_{sanitizedFileName}";
    }

    public static string ComputeFingerprint(Guid tenantId, CreateUploadSessionCommand cmd)
    {
        using var sha = SHA256.Create();
        var raw = $"{tenantId:D}|{cmd.ObjectType.Trim().ToUpperInvariant()}|{cmd.ObjectId:D}|{cmd.FileName.Trim().ToLowerInvariant()}|{cmd.ContentType.Trim().ToLowerInvariant()}|{cmd.SizeBytes}";
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}

public sealed class AttachmentService(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAttachmentStore store,
    IAttachmentReader reader,
    IObjectStorageProvider storageProvider)
{
    public async Task<UploadSessionResult> CreateUploadSessionAsync(
        CreateUploadSessionCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("attachments.upload", new(tenantId), cancellationToken: cancellationToken);

        var objectType = AttachmentRules.ParseObjectType(command.ObjectType);
        if (command.ObjectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.OBJECT_ID_REQUIRED", "Object ID is required.");

        var fileName = AttachmentRules.SanitizeFileName(command.FileName);

        if (command.SizeBytes <= 0)
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.SIZE_INVALID", "File size must be greater than zero.");

        var maxSizeBytes = await store.GetTenantMaxUploadSizeAsync(tenantId, cancellationToken) ?? Attachment.MaxFileSizeBytes;
        if (command.SizeBytes > maxSizeBytes)
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.SIZE_EXCEEDED",
                $"File size {command.SizeBytes} bytes exceeds maximum allowed size of {maxSizeBytes} bytes (500 MB).");

        var customAllowlist = await store.GetTenantAllowedContentTypesAsync(tenantId, cancellationToken);
        if (!AttachmentRules.IsContentTypeAllowed(command.ContentType, customAllowlist))
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.TYPE_NOT_ALLOWED",
                $"File content type '{command.ContentType}' is not permitted by tenant policy.");

        string? keyHash = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var trimmedKey = idempotencyKey.Trim();
            if (trimmedKey.Length < 1 || trimmedKey.Length > 128 || trimmedKey.Any(c => c < 32 || c > 126))
                throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Idempotency-Key must consist of 1-128 visible ASCII characters.");

            using var sha = SHA256.Create();
            keyHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(trimmedKey))).ToLowerInvariant();
        }

        var fingerprint = AttachmentRules.ComputeFingerprint(tenantId, command);

        await using var tx = await store.BeginCreateSessionAsync(tenantId, actorId, keyHash, cancellationToken);

        if (keyHash is not null)
        {
            var replay = await tx.FindReplayAsync(cancellationToken);
            if (replay is not null)
            {
                if (replay.Fingerprint != fingerprint)
                    throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT",
                        "Idempotency key has already been used with different request parameters.");
                return replay.Result;
            }
        }

        var targetExists = await tx.ValidateTargetExistsAsync(objectType, command.ObjectId, cancellationToken);
        if (!targetExists)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.TARGET_NOT_FOUND",
                $"Referenced {command.ObjectType} was not found in the current tenant.");

        var attachmentId = Guid.CreateVersion7();
        var objectKey = AttachmentRules.BuildObjectKey(tenantId, attachmentId, fileName);
        var now = DateTimeOffset.UtcNow;

        var attachment = Attachment.CreateUploadSession(
            tenantId,
            objectType,
            command.ObjectId,
            actorId,
            fileName,
            command.ContentType,
            command.SizeBytes,
            objectKey,
            now,
            attachmentId);

        var audit = AuditLog.AttachmentUploadSessionCreated(attachment, now, keyHash, keyHash is null ? null : fingerprint);

        await tx.CommitAsync(attachment, audit, cancellationToken);

        var sessionDescriptor = await storageProvider.CreateUploadSessionAsync(
            attachment.Id,
            objectKey,
            command.ContentType,
            command.SizeBytes,
            TimeSpan.FromHours(1),
            cancellationToken);

        return new UploadSessionResult(
            attachment.Id,
            attachment.ObjectKey,
            sessionDescriptor.UploadUrl,
            sessionDescriptor.ExpiresAt);
    }

    public async Task DirectBinaryUploadAsync(
        Guid attachmentId,
        Stream contentStream,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        var attachment = await store.FindAttachmentAsync(tenantId, attachmentId, cancellationToken);
        if (attachment is null)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment not found.");

        if (attachment.UploadedBy != actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ATTACHMENT.FORBIDDEN", "Only the original uploader can stream the binary payload.");

        if (attachment.Status != AttachmentStatus.Uploading)
            throw new ApplicationFault(FaultKind.Conflict, "ATTACHMENT.INVALID_STATUS",
                $"Cannot upload binary for attachment with status {attachment.Status}.");

        await storageProvider.SaveBinaryDirectAsync(attachment.ObjectKey, contentStream, cancellationToken);
    }

    public async Task<AttachmentItemView> FinalizeAttachmentAsync(
        FinalizeAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("attachments.upload", new(tenantId), cancellationToken: cancellationToken);

        var attachment = await store.FindAttachmentAsync(tenantId, command.AttachmentId, cancellationToken);
        if (attachment is null)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment was not found.");

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        if (attachment.UploadedBy != actorId && !isAdmin)
            throw new ApplicationFault(FaultKind.Forbidden, "ATTACHMENT.FORBIDDEN", "Only the uploader or company administrator can finalize the attachment.");

        if (attachment.Status != AttachmentStatus.Uploading)
            throw new ApplicationFault(FaultKind.Conflict, "ATTACHMENT.ALREADY_FINALIZED",
                $"Attachment is already in status {attachment.Status}.");

        var inspection = await storageProvider.InspectFileAsync(attachment.ObjectKey, cancellationToken);
        if (!inspection.Exists)
        {
            attachment.FailUpload(DateTimeOffset.UtcNow);
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.STORAGE_FILE_MISSING",
                "Storage file was not found. Please upload the binary before finalizing.");
        }

        var maxSizeBytes = await store.GetTenantMaxUploadSizeAsync(tenantId, cancellationToken) ?? Attachment.MaxFileSizeBytes;
        if (inspection.SizeBytes <= 0 || inspection.SizeBytes > maxSizeBytes)
        {
            attachment.FailUpload(DateTimeOffset.UtcNow);
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.SIZE_EXCEEDED",
                $"Actual stored file size {inspection.SizeBytes} bytes violates size limits (1..{maxSizeBytes}).");
        }

        if (!string.IsNullOrWhiteSpace(command.ClientHash))
        {
            var expected = command.ClientHash.Trim().ToLowerInvariant();
            if (!string.Equals(expected, inspection.HashSha256, StringComparison.OrdinalIgnoreCase))
            {
                attachment.FailUpload(DateTimeOffset.UtcNow);
                throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.HASH_MISMATCH",
                    "Client provided hash did not match computed storage hash.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        attachment.FinalizeUpload(inspection.HashSha256, inspection.SizeBytes, now);

        var audit = AuditLog.AttachmentFinalized(attachment, now);
        await store.FinalizeAttachmentAsync(attachment, audit, cancellationToken);

        var view = await reader.GetAttachmentViewAsync(tenantId, attachment.Id, actorId, isAdmin, cancellationToken);
        return view ?? throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment view not found.");
    }

    public async Task<IReadOnlyList<AttachmentItemView>> ListAttachmentsAsync(
        string objectTypeString,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("attachments.read", new(tenantId), cancellationToken: cancellationToken);

        var objectType = AttachmentRules.ParseObjectType(objectTypeString);
        if (objectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "ATTACHMENT.OBJECT_ID_REQUIRED", "Object ID is required.");

        var targetExists = await reader.TargetExistsAsync(tenantId, objectType, objectId, cancellationToken);
        if (!targetExists)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.TARGET_NOT_FOUND", "Referenced target object not found.");

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        return await reader.ListAttachmentsAsync(tenantId, objectType, objectId, actorId, isAdmin, cancellationToken);
    }

    public async Task<AttachmentDownloadDescriptor> GetDownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("attachments.read", new(tenantId), cancellationToken: cancellationToken);

        var attachment = await store.FindAttachmentAsync(tenantId, attachmentId, cancellationToken);
        if (attachment is null || attachment.Status != AttachmentStatus.Ready || attachment.DeletedAt is not null)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment not found or not ready.");

        var stream = await storageProvider.OpenReadAsync(attachment.ObjectKey, cancellationToken);
        if (stream is null)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.CONTENT_NOT_FOUND", "Storage object content was not found.");

        return new AttachmentDownloadDescriptor(
            stream,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes);
    }

    public async Task DeleteAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Active tenant workspace is required.");

        await authorizer.AuthorizeAsync("attachments.delete", new(tenantId), cancellationToken: cancellationToken);

        var attachment = await store.FindAttachmentAsync(tenantId, attachmentId, cancellationToken);
        if (attachment is null || attachment.DeletedAt is not null)
            throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment was not found.");

        var isAdmin = await store.IsUserAdminAsync(tenantId, actorId, cancellationToken);
        if (attachment.UploadedBy != actorId && !isAdmin)
            throw new ApplicationFault(FaultKind.Forbidden, "ATTACHMENT.FORBIDDEN",
                "Only the original uploader or a company administrator can delete this attachment.");

        var now = DateTimeOffset.UtcNow;
        attachment.Delete(now);

        var audit = AuditLog.AttachmentDeleted(attachment, actorId, now);
        await store.DeleteAttachmentAsync(attachment, audit, cancellationToken);
    }
}
