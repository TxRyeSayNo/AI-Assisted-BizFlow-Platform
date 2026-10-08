using System.Text.Json;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Collaboration;

public sealed class AttachmentTests
{
    [Fact]
    public void Attachment_CreateUploadSession_validates_required_fields_and_initializes_state()
    {
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var attachment = Attachment.CreateUploadSession(
            tenantId,
            AttachmentObjectType.Task,
            objectId,
            uploaderId,
            "  report.pdf  ",
            "APPLICATION/PDF",
            1024 * 1024,
            "tenants/test/report.pdf",
            now);

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(tenantId, attachment.TenantId);
        Assert.Equal(AttachmentObjectType.Task, attachment.ObjectType);
        Assert.Equal(objectId, attachment.ObjectId);
        Assert.Equal(uploaderId, attachment.UploadedBy);
        Assert.Equal("report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1024 * 1024, attachment.SizeBytes);
        Assert.Equal("tenants/test/report.pdf", attachment.ObjectKey);
        Assert.Equal(string.Empty, attachment.Hash);
        Assert.Equal(AttachmentStatus.Uploading, attachment.Status);
        Assert.Equal(now, attachment.CreatedAt);
        Assert.Null(attachment.DeletedAt);
    }

    [Fact]
    public void Attachment_CreateUploadSession_throws_on_invalid_arguments()
    {
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            Guid.Empty, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "application/pdf", 100, "key"));
        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, Guid.Empty, uploaderId, "file.pdf", "application/pdf", 100, "key"));
        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, Guid.Empty, "file.pdf", "application/pdf", 100, "key"));
        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "", "application/pdf", 100, "key"));
        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "", 100, "key"));
        Assert.Throws<ArgumentException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "application/pdf", 100, ""));

        // Size <= 0
        Assert.Throws<ArgumentOutOfRangeException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "application/pdf", 0, "key"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "application/pdf", -5, "key"));

        // Size > 500 MB (524,288,000 bytes) - TST-FILE-002
        Assert.Throws<ArgumentOutOfRangeException>(() => Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, uploaderId, "file.pdf", "application/pdf", Attachment.MaxFileSizeBytes + 1, "key"));
    }

    [Fact]
    public void Attachment_FinalizeUpload_transitions_to_Ready_and_records_hash()
    {
        var attachment = Attachment.CreateUploadSession(
            Guid.NewGuid(), AttachmentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(),
            "data.xlsx", "application/vnd.ms-excel", 5000, "key");

        var hash = new string('a', 64);
        attachment.FinalizeUpload(hash, 5050);

        Assert.Equal(AttachmentStatus.Ready, attachment.Status);
        Assert.Equal(hash, attachment.Hash);
        Assert.Equal(5050, attachment.SizeBytes);
    }

    [Fact]
    public void Attachment_FinalizeUpload_throws_if_not_uploading_or_invalid()
    {
        var attachment = Attachment.CreateUploadSession(
            Guid.NewGuid(), AttachmentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(),
            "data.xlsx", "application/vnd.ms-excel", 5000, "key");

        attachment.FinalizeUpload(new string('a', 64), 5000);

        // Cannot finalize twice
        Assert.Throws<InvalidOperationException>(() => attachment.FinalizeUpload(new string('a', 64), 5000));

        var another = Attachment.CreateUploadSession(
            Guid.NewGuid(), AttachmentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(),
            "data.xlsx", "application/vnd.ms-excel", 5000, "key");
        another.Delete();

        // Cannot finalize deleted
        Assert.Throws<InvalidOperationException>(() => another.FinalizeUpload(new string('a', 64), 5000));
    }

    [Fact]
    public void Attachment_Delete_sets_DeletedAt_and_status()
    {
        var attachment = Attachment.CreateUploadSession(
            Guid.NewGuid(), AttachmentObjectType.Comment, Guid.NewGuid(), Guid.NewGuid(),
            "image.png", "image/png", 200, "key");

        var deleteTime = DateTimeOffset.UtcNow;
        attachment.Delete(deleteTime);

        Assert.Equal(AttachmentStatus.Deleted, attachment.Status);
        Assert.Equal(deleteTime, attachment.DeletedAt);

        // Idempotent delete
        attachment.Delete(deleteTime.AddMinutes(1));
        Assert.Equal(deleteTime, attachment.DeletedAt);
    }

    [Fact]
    public void Attachment_Audit_methods_generate_structured_logs()
    {
        var attachment = Attachment.CreateUploadSession(
            Guid.NewGuid(), AttachmentObjectType.Task, Guid.NewGuid(), Guid.NewGuid(),
            "diagram.png", "image/png", 1024, "key");

        var now = DateTimeOffset.UtcNow;
        var keyHash = new string('0', 64);
        var fingerprint = new string('1', 64);

        var createdAudit = AuditLog.AttachmentUploadSessionCreated(attachment, now, keyHash, fingerprint);
        Assert.Equal("ATTACHMENT.UPLOAD_SESSION_CREATED", createdAudit.Action);
        Assert.Equal(attachment.TenantId, createdAudit.TenantId);
        Assert.Equal(attachment.UploadedBy, createdAudit.ActorId);
        Assert.NotNull(createdAudit.MetadataJson);

        attachment.FinalizeUpload(new string('2', 64), 1024, now);
        var finalizedAudit = AuditLog.AttachmentFinalized(attachment, now);
        Assert.Equal("ATTACHMENT.FINALIZED", finalizedAudit.Action);
        Assert.Contains("READY", finalizedAudit.AfterJson);

        var deleter = Guid.NewGuid();
        attachment.Delete(now);
        var deletedAudit = AuditLog.AttachmentDeleted(attachment, deleter, now);
        Assert.Equal("ATTACHMENT.DELETED", deletedAudit.Action);
        Assert.Equal(deleter, deletedAudit.ActorId);
        Assert.Contains("DELETED", deletedAudit.AfterJson);
    }

    [Fact]
    public void AttachmentRules_ParseObjectType_handles_all_cases()
    {
        Assert.Equal(AttachmentObjectType.Task, AttachmentRules.ParseObjectType("TASK"));
        Assert.Equal(AttachmentObjectType.Request, AttachmentRules.ParseObjectType("request"));
        Assert.Equal(AttachmentObjectType.Comment, AttachmentRules.ParseObjectType("Comment"));
        Assert.Equal(AttachmentObjectType.Result, AttachmentRules.ParseObjectType("RESULT"));
        Assert.Equal(AttachmentObjectType.Progress, AttachmentRules.ParseObjectType("progress"));

        Assert.Throws<ApplicationFault>(() => AttachmentRules.ParseObjectType(""));
        Assert.Throws<ApplicationFault>(() => AttachmentRules.ParseObjectType("INVALID"));
    }

    [Fact]
    public void AttachmentRules_IsContentTypeAllowed_validates_allowlist()
    {
        Assert.True(AttachmentRules.IsContentTypeAllowed("application/pdf", null));
        Assert.True(AttachmentRules.IsContentTypeAllowed("image/png", null));
        Assert.True(AttachmentRules.IsContentTypeAllowed("text/csv", null));
        Assert.True(AttachmentRules.IsContentTypeAllowed("application/zip", null));

        Assert.False(AttachmentRules.IsContentTypeAllowed("application/x-msdownload", null));
        Assert.False(AttachmentRules.IsContentTypeAllowed("application/x-sh", null));
        Assert.False(AttachmentRules.IsContentTypeAllowed("", null));

        // Custom allowlist
        var custom = new[] { "application/pdf" };
        Assert.True(AttachmentRules.IsContentTypeAllowed("application/pdf", custom));
        Assert.False(AttachmentRules.IsContentTypeAllowed("image/png", custom));
    }

    [Fact]
    public async Task AttachmentService_CreateUploadSessionAsync_authorizes_and_creates_session()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var objectId = Guid.NewGuid();

        var context = new MockTenantContext(tenantId, actorId);
        var authorizer = new MockAuthorizer();
        var store = new MockAttachmentStore(targetExists: true);
        var reader = new MockAttachmentReader();
        var storage = new MockStorageProvider();

        var service = new AttachmentService(context, authorizer, store, reader, storage);

        var cmd = new CreateUploadSessionCommand("TASK", objectId, "spec.pdf", "application/pdf", 10240);
        var result = await service.CreateUploadSessionAsync(cmd, null, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.AttachmentId);
        Assert.Contains("spec.pdf", result.ObjectKey);
        Assert.Contains("upload", result.UploadUrl);
        Assert.Contains("attachments.upload", authorizer.PermissionsChecked);
    }

    [Fact]
    public async Task AttachmentService_CreateUploadSessionAsync_rejects_exceeded_size()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var objectId = Guid.NewGuid();

        var context = new MockTenantContext(tenantId, actorId);
        var authorizer = new MockAuthorizer();
        var store = new MockAttachmentStore(targetExists: true);
        var reader = new MockAttachmentReader();
        var storage = new MockStorageProvider();

        var service = new AttachmentService(context, authorizer, store, reader, storage);

        // 500 MB + 1 byte
        var cmd = new CreateUploadSessionCommand("TASK", objectId, "large.zip", "application/zip", Attachment.MaxFileSizeBytes + 1);
        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateUploadSessionAsync(cmd, null, CancellationToken.None));
        Assert.Equal("ATTACHMENT.SIZE_EXCEEDED", ex.Code);
    }

    [Fact]
    public async Task AttachmentService_FinalizeAttachmentAsync_validates_storage_and_completes()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var context = new MockTenantContext(tenantId, actorId);
        var authorizer = new MockAuthorizer();
        var store = new MockAttachmentStore(targetExists: true);
        var reader = new MockAttachmentReader();
        var storage = new MockStorageProvider
        {
            InspectionResult = new StorageFileInspection(true, 5000, "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")
        };

        var attachment = Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, actorId,
            "evidence.png", "image/png", 5000, "key", DateTimeOffset.UtcNow, attachmentId);
        store.Attachments[attachmentId] = attachment;

        var service = new AttachmentService(context, authorizer, store, reader, storage);

        var finalizeCmd = new FinalizeAttachmentCommand(attachmentId, "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
        var view = await service.FinalizeAttachmentAsync(finalizeCmd, CancellationToken.None);

        Assert.Equal(AttachmentStatus.Ready, attachment.Status);
        Assert.Equal(attachmentId, view.AttachmentId);
        Assert.True(store.FinalizedCalled);
    }

    [Fact]
    public async Task AttachmentService_FinalizeAttachmentAsync_rejects_hash_mismatch()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var context = new MockTenantContext(tenantId, actorId);
        var authorizer = new MockAuthorizer();
        var store = new MockAttachmentStore(targetExists: true);
        var reader = new MockAttachmentReader();
        var storage = new MockStorageProvider
        {
            InspectionResult = new StorageFileInspection(true, 5000, new string('a', 64))
        };

        var attachment = Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, objectId, actorId,
            "evidence.png", "image/png", 5000, "key", DateTimeOffset.UtcNow, attachmentId);
        store.Attachments[attachmentId] = attachment;

        var service = new AttachmentService(context, authorizer, store, reader, storage);

        var finalizeCmd = new FinalizeAttachmentCommand(attachmentId, new string('b', 64)); // mismatch
        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.FinalizeAttachmentAsync(finalizeCmd, CancellationToken.None));
        Assert.Equal("ATTACHMENT.HASH_MISMATCH", ex.Code);
        Assert.Equal(AttachmentStatus.Failed, attachment.Status);
    }

    [Fact]
    public async Task AttachmentService_DeleteAttachmentAsync_requires_uploader_or_admin()
    {
        var tenantId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var bystanderId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var attachment = Attachment.CreateUploadSession(
            tenantId, AttachmentObjectType.Task, Guid.NewGuid(), uploaderId,
            "test.pdf", "application/pdf", 100, "key", DateTimeOffset.UtcNow, attachmentId);

        // Bystander attempting delete
        var context = new MockTenantContext(tenantId, bystanderId);
        var authorizer = new MockAuthorizer();
        var store = new MockAttachmentStore(targetExists: true) { IsAdmin = false };
        store.Attachments[attachmentId] = attachment;
        var reader = new MockAttachmentReader();
        var storage = new MockStorageProvider();

        var service = new AttachmentService(context, authorizer, store, reader, storage);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.DeleteAttachmentAsync(attachmentId, CancellationToken.None));
        Assert.Equal("ATTACHMENT.FORBIDDEN", ex.Code);

        // Admin attempting delete
        store.IsAdmin = true;
        await service.DeleteAttachmentAsync(attachmentId, CancellationToken.None);
        Assert.Equal(AttachmentStatus.Deleted, attachment.Status);
        Assert.True(store.DeletedCalled);
    }

    private sealed class MockTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public bool IsPlatformAdmin => false;
        public string? Email => "user@example.com";
    }

    private sealed class MockAuthorizer : IResourceAuthorizer
    {
        public List<string> PermissionsChecked { get; } = [];

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            PermissionsChecked.Add(permission);
            return Task.CompletedTask;
        }
    }

    private sealed class MockAttachmentStore(bool targetExists) : IAttachmentStore
    {
        public Dictionary<Guid, Attachment> Attachments { get; } = [];
        public bool IsAdmin { get; set; }
        public bool FinalizedCalled { get; private set; }
        public bool DeletedCalled { get; private set; }

        public Task<IAttachmentCreationTransaction> BeginCreateSessionAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAttachmentCreationTransaction>(new MockTx(this, targetExists));
        }

        public Task<Attachment?> FindAttachmentAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken)
        {
            Attachments.TryGetValue(attachmentId, out var a);
            return Task.FromResult(a);
        }

        public Task FinalizeAttachmentAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
        {
            FinalizedCalled = true;
            return Task.CompletedTask;
        }

        public Task DeleteAttachmentAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
        {
            DeletedCalled = true;
            return Task.CompletedTask;
        }

        public Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsAdmin);
        }

        public Task<long?> GetTenantMaxUploadSizeAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<long?>(null);
        }

        public Task<IReadOnlyList<string>?> GetTenantAllowedContentTypesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }

        private sealed class MockTx(MockAttachmentStore store, bool targetExists) : IAttachmentCreationTransaction
        {
            public Task<StoredUploadSessionCreation?> FindReplayAsync(CancellationToken cancellationToken) => Task.FromResult<StoredUploadSessionCreation?>(null);
            public Task<bool> ValidateTargetExistsAsync(AttachmentObjectType objectType, Guid objectId, CancellationToken cancellationToken) => Task.FromResult(targetExists);
            public Task CommitAsync(Attachment attachment, AuditLog audit, CancellationToken cancellationToken)
            {
                store.Attachments[attachment.Id] = attachment;
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class MockAttachmentReader : IAttachmentReader
    {
        public Task<IReadOnlyList<AttachmentItemView>> ListAttachmentsAsync(Guid tenantId, AttachmentObjectType objectType, Guid objectId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AttachmentItemView>>([]);
        }

        public Task<AttachmentItemView?> GetAttachmentViewAsync(Guid tenantId, Guid attachmentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken)
        {
            return Task.FromResult<AttachmentItemView?>(new AttachmentItemView(
                attachmentId, "TASK", Guid.NewGuid(), currentUserId, "Test User", "test@example.com",
                "file.pdf", "application/pdf", 5000, "READY", new string('a', 64), DateTimeOffset.UtcNow,
                true, true));
        }

        public Task<bool> TargetExistsAsync(Guid tenantId, AttachmentObjectType objectType, Guid objectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class MockStorageProvider : IObjectStorageProvider
    {
        public StorageFileInspection InspectionResult { get; set; } = new(true, 1024, new string('0', 64));

        public Task<UploadSessionDescriptor> CreateUploadSessionAsync(Guid attachmentId, string objectKey, string contentType, long sizeBytes, TimeSpan expiry, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UploadSessionDescriptor($"/api/v1/attachments/upload-session/{attachmentId:D}/binary", DateTimeOffset.UtcNow.AddHours(1)));
        }

        public Task SaveBinaryDirectAsync(string objectKey, Stream content, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StorageFileInspection> InspectFileAsync(string objectKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(InspectionResult);
        }

        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        }

        public Task<bool> DeleteFileAsync(string objectKey, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
