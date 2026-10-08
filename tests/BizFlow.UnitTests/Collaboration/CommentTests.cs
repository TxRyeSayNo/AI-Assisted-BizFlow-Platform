using System.Text.Json;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Collaboration;

public sealed class CommentTests
{
    [Fact]
    public void Comment_Create_validates_required_fields_and_trims_content()
    {
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var comment = Comment.Create(tenantId, CommentObjectType.Task, objectId, authorId, "  Hello, world!  ", now);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(tenantId, comment.TenantId);
        Assert.Equal(CommentObjectType.Task, comment.ObjectType);
        Assert.Equal(objectId, comment.ObjectId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal("Hello, world!", comment.Content);
        Assert.Equal(now, comment.CreatedAt);
        Assert.Null(comment.EditedAt);
        Assert.Null(comment.DeletedAt);
    }

    [Fact]
    public void Comment_Create_throws_on_invalid_arguments()
    {
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Comment.Create(Guid.Empty, CommentObjectType.Task, objectId, authorId, "Content"));
        Assert.Throws<ArgumentException>(() => Comment.Create(tenantId, CommentObjectType.Task, Guid.Empty, authorId, "Content"));
        Assert.Throws<ArgumentException>(() => Comment.Create(tenantId, CommentObjectType.Task, objectId, Guid.Empty, "Content"));
        Assert.Throws<ArgumentException>(() => Comment.Create(tenantId, CommentObjectType.Task, objectId, authorId, ""));
        Assert.Throws<ArgumentException>(() => Comment.Create(tenantId, CommentObjectType.Task, objectId, authorId, "   "));
        Assert.Throws<ArgumentException>(() => Comment.Create(tenantId, CommentObjectType.Task, objectId, authorId, new string('A', 4001)));
    }

    [Fact]
    public void Comment_Edit_updates_content_and_timestamp()
    {
        var comment = Comment.Create(Guid.NewGuid(), CommentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(), "Initial");
        var editedTime = DateTimeOffset.UtcNow.AddMinutes(5);

        comment.Edit("Updated text", editedTime);

        Assert.Equal("Updated text", comment.Content);
        Assert.Equal(editedTime, comment.EditedAt);
    }

    [Fact]
    public void Comment_Edit_fails_when_deleted_or_invalid()
    {
        var comment = Comment.Create(Guid.NewGuid(), CommentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(), "Initial");
        comment.Delete();

        Assert.Throws<InvalidOperationException>(() => comment.Edit("New text"));

        var activeComment = Comment.Create(Guid.NewGuid(), CommentObjectType.Request, Guid.NewGuid(), Guid.NewGuid(), "Initial");
        Assert.Throws<ArgumentException>(() => activeComment.Edit(""));
        Assert.Throws<ArgumentException>(() => activeComment.Edit(new string('X', 4001)));
    }

    [Fact]
    public void Comment_Delete_sets_timestamp_and_is_idempotent()
    {
        var comment = Comment.Create(Guid.NewGuid(), CommentObjectType.Task, Guid.NewGuid(), Guid.NewGuid(), "Initial");
        var deleteTime = DateTimeOffset.UtcNow;

        comment.Delete(deleteTime);
        Assert.Equal(deleteTime, comment.DeletedAt);

        comment.Delete(deleteTime.AddHours(1));
        Assert.Equal(deleteTime, comment.DeletedAt);
    }

    [Fact]
    public void AuditLog_CommentCreated_creates_structured_snapshot()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var comment = Comment.Create(tenantId, CommentObjectType.Task, Guid.NewGuid(), authorId, "Test comment", now);
        var keyHash = new string('a', 64);
        var fingerprint = new string('b', 64);

        var audit = AuditLog.CommentCreated(comment, now, keyHash, fingerprint);

        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(authorId, audit.ActorId);
        Assert.Equal("Comment", audit.ObjectType);
        Assert.Equal(comment.Id, audit.ObjectId);
        Assert.Equal("COMMENT.CREATED", audit.Action);

        using var afterDoc = JsonDocument.Parse(audit.AfterJson!);
        var root = afterDoc.RootElement;
        Assert.Equal(comment.Id, root.GetProperty("commentId").GetGuid());
        Assert.Equal("TASK", root.GetProperty("objectType").GetString());
        Assert.Equal("Test comment", root.GetProperty("content").GetString());

        using var metaDoc = JsonDocument.Parse(audit.MetadataJson!);
        Assert.Equal(keyHash, metaDoc.RootElement.GetProperty("idempotency").GetProperty("keyHash").GetString());
    }

    [Fact]
    public void AuditLog_CommentEdited_and_Deleted_record_actions()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var comment = Comment.Create(tenantId, CommentObjectType.Request, Guid.NewGuid(), authorId, "Before text", now);

        comment.Edit("After text", now.AddMinutes(2));
        var editAudit = AuditLog.CommentEdited(comment, "Before text", now.AddMinutes(2));
        Assert.Equal("COMMENT.EDITED", editAudit.Action);
        Assert.Equal(comment.Id, editAudit.ObjectId);

        comment.Delete(now.AddMinutes(5));
        var deleteAudit = AuditLog.CommentDeleted(comment, adminId, now.AddMinutes(5));
        Assert.Equal("COMMENT.DELETED", deleteAudit.Action);
        Assert.Equal(adminId, deleteAudit.ActorId);
    }

    [Fact]
    public async Task CommentService_CreateAsync_validates_and_persists()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var context = new TestTenantContext(tenantId, actorId);
        var authorizer = new TestAuthorizer();
        var store = new MockCommentStore();
        store.Targets.Add((CommentObjectType.Task, taskId));
        var reader = new MockCommentReader(store);

        var service = new CommentService(context, authorizer, store, reader, TimeProvider.System);
        var view = await service.CreateAsync(new("TASK", taskId, "New collaboration comment"), null, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("New collaboration comment", view.Content);
        Assert.True(store.CreatedComments.Count == 1);
        Assert.Equal(taskId, store.CreatedComments[0].ObjectId);
    }

    [Fact]
    public async Task CommentService_CreateAsync_throws_when_target_missing()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var context = new TestTenantContext(tenantId, actorId);
        var authorizer = new TestAuthorizer();
        var store = new MockCommentStore(); // no targets registered
        var reader = new MockCommentReader(store);

        var service = new CommentService(context, authorizer, store, reader, TimeProvider.System);
        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.CreateAsync(new("TASK", Guid.NewGuid(), "Comment on ghost"), null, CancellationToken.None));

        Assert.Equal(FaultKind.NotFound, fault.Kind);
    }

    [Fact]
    public async Task CommentService_EditAsync_enforces_author_ownership()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var comment = Comment.Create(tenantId, CommentObjectType.Task, Guid.NewGuid(), authorId, "Original");

        var store = new MockCommentStore();
        store.ExistingComments[comment.Id] = comment;
        var reader = new MockCommentReader(store);

        // Caller is otherUserId
        var context = new TestTenantContext(tenantId, otherUserId);
        var authorizer = new TestAuthorizer();
        var service = new CommentService(context, authorizer, store, reader, TimeProvider.System);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.EditAsync(comment.Id, new("Malicious edit"), CancellationToken.None));

        Assert.Equal(FaultKind.Forbidden, fault.Kind);
        Assert.Equal("COMMENT.NOT_OWNER", fault.Code);
    }

    [Fact]
    public async Task CommentService_DeleteAsync_allows_author_or_admin_and_rejects_third_party()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var bystanderId = Guid.NewGuid();
        var comment = Comment.Create(tenantId, CommentObjectType.Task, Guid.NewGuid(), authorId, "Original");

        var store = new MockCommentStore();
        store.ExistingComments[comment.Id] = comment;
        store.Admins.Add(adminId);
        var reader = new MockCommentReader(store);

        // Bystander attempt fails
        var bystanderContext = new TestTenantContext(tenantId, bystanderId);
        var bystanderService = new CommentService(bystanderContext, new TestAuthorizer(), store, reader, TimeProvider.System);
        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            bystanderService.DeleteAsync(comment.Id, CancellationToken.None));
        Assert.Equal(FaultKind.Forbidden, fault.Kind);

        // Admin attempt succeeds
        var adminContext = new TestTenantContext(tenantId, adminId);
        var adminService = new CommentService(adminContext, new TestAuthorizer(), store, reader, TimeProvider.System);
        await adminService.DeleteAsync(comment.Id, CancellationToken.None);
        Assert.NotNull(comment.DeletedAt);
    }

    private sealed class TestTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
    }

    private sealed class TestAuthorizer : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MockCommentStore : ICommentStore
    {
        public readonly List<Comment> CreatedComments = [];
        public readonly Dictionary<Guid, Comment> ExistingComments = [];
        public readonly HashSet<Guid> Admins = [];
        public readonly HashSet<(CommentObjectType, Guid)> Targets = [];

        public Task<ICommentCreationTransaction> BeginCreateAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
        {
            return Task.FromResult<ICommentCreationTransaction>(new MockCreationTransaction(this));
        }

        public Task<Comment?> FindCommentAsync(Guid tenantId, Guid commentId, CancellationToken cancellationToken)
        {
            ExistingComments.TryGetValue(commentId, out var c);
            return Task.FromResult(c);
        }

        public Task UpdateCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCommentAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Admins.Contains(userId));

        private sealed class MockCreationTransaction(MockCommentStore store) : ICommentCreationTransaction
        {
            public Task<StoredCommentCreation?> FindReplayAsync(CancellationToken cancellationToken) => Task.FromResult<StoredCommentCreation?>(null);

            public Task<bool> ValidateTargetExistsAsync(CommentObjectType objectType, Guid objectId, CancellationToken cancellationToken) =>
                Task.FromResult(store.Targets.Contains((objectType, objectId)));

            public Task CommitAsync(Comment comment, AuditLog audit, CancellationToken cancellationToken)
            {
                store.CreatedComments.Add(comment);
                store.ExistingComments[comment.Id] = comment;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class MockCommentReader(MockCommentStore store) : ICommentReader
    {
        public Task<IReadOnlyList<CommentItemView>> ListCommentsAsync(
            Guid tenantId, CommentObjectType objectType, Guid objectId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken)
        {
            var list = store.ExistingComments.Values
                .Where(c => c.TenantId == tenantId && c.ObjectType == objectType && c.ObjectId == objectId && c.DeletedAt == null)
                .Select(c => new CommentItemView(c.Id, c.ObjectType.ToString().ToUpperInvariant(), c.ObjectId, c.AuthorId, "Test User", "test@example.com", c.Content, c.CreatedAt, c.EditedAt, c.AuthorId == currentUserId, c.AuthorId == currentUserId || isAdmin))
                .ToArray();
            return Task.FromResult<IReadOnlyList<CommentItemView>>(list);
        }

        public Task<CommentItemView?> GetCommentByIdAsync(
            Guid tenantId, Guid commentId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken)
        {
            if (!store.ExistingComments.TryGetValue(commentId, out var c) || c.DeletedAt != null)
                return Task.FromResult<CommentItemView?>(null);

            return Task.FromResult<CommentItemView?>(new CommentItemView(
                c.Id, c.ObjectType.ToString().ToUpperInvariant(), c.ObjectId, c.AuthorId, "Test User", "test@example.com", c.Content, c.CreatedAt, c.EditedAt, c.AuthorId == currentUserId, c.AuthorId == currentUserId || isAdmin));
        }
    }
}
