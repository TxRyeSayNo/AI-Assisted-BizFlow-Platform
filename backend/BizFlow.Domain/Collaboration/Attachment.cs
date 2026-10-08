using BizFlow.Domain.Common;

namespace BizFlow.Domain.Collaboration;

public sealed class Attachment
{
    public const long MaxFileSizeBytes = 524_288_000; // 500 MB hard ceiling (BR-014, SSS §29 Appendix C)

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public AttachmentObjectType ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public AttachmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Attachment() { }

    public static Attachment CreateUploadSession(
        Guid tenantId,
        AttachmentObjectType objectType,
        Guid objectId,
        Guid uploadedBy,
        string fileName,
        string contentType,
        long sizeBytes,
        string objectKey,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (objectId == Guid.Empty)
            throw new ArgumentException("Object ID is required.", nameof(objectId));
        if (uploadedBy == Guid.Empty)
            throw new ArgumentException("Uploader ID is required.", nameof(uploadedBy));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key cannot be empty.", nameof(objectKey));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size must be greater than zero.");
        if (sizeBytes > MaxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), $"File size cannot exceed {MaxFileSizeBytes} bytes (500 MB).");

        var sanitizedFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
            throw new ArgumentException("File name is invalid.", nameof(fileName));
        if (sanitizedFileName.Length > 255)
            sanitizedFileName = sanitizedFileName[..255];

        var trimmedContentType = contentType.Trim().ToLowerInvariant();
        if (trimmedContentType.Length > 100)
            trimmedContentType = trimmedContentType[..100];

        var trimmedObjectKey = objectKey.Trim();
        if (trimmedObjectKey.Length > 500)
            throw new ArgumentException("Object key cannot exceed 500 characters.", nameof(objectKey));

        return new Attachment
        {
            Id = id ?? Guid.CreateVersion7(),
            TenantId = tenantId,
            ObjectType = objectType,
            ObjectId = objectId,
            UploadedBy = uploadedBy,
            FileName = sanitizedFileName,
            ContentType = trimmedContentType,
            SizeBytes = sizeBytes,
            ObjectKey = trimmedObjectKey,
            Hash = string.Empty,
            Status = AttachmentStatus.Uploading,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            DeletedAt = null
        };
    }

    public void FinalizeUpload(string hash, long actualSizeBytes, DateTimeOffset? finalizedAt = null)
    {
        if (DeletedAt != null)
            throw new InvalidOperationException("Cannot finalize a deleted attachment.");
        if (Status != AttachmentStatus.Uploading)
            throw new InvalidOperationException($"Cannot finalize attachment with status {Status}. Must be Uploading.");
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Content hash is required to finalize attachment.", nameof(hash));
        if (actualSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(actualSizeBytes), "Actual file size must be greater than zero.");
        if (actualSizeBytes > MaxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(actualSizeBytes), $"Actual file size cannot exceed {MaxFileSizeBytes} bytes (500 MB).");

        Hash = hash.Trim();
        SizeBytes = actualSizeBytes;
        Status = AttachmentStatus.Ready;
    }

    public void FailUpload(DateTimeOffset? failedAt = null)
    {
        if (DeletedAt != null || Status == AttachmentStatus.Ready)
            return;
        Status = AttachmentStatus.Failed;
    }

    public void Delete(DateTimeOffset? deletedAt = null)
    {
        if (DeletedAt != null)
            return;
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
        Status = AttachmentStatus.Deleted;
    }
}
