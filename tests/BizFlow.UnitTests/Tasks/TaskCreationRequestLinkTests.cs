using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskCreationRequestLinkTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), User = Guid.NewGuid(), Request = Guid.NewGuid();

    [Fact]
    public async Task Creating_task_with_valid_request_records_request_link()
    {
        var store = new RequestAwareStore(isValidRequest: true);
        var authority = new GrantAuthorizer();
        var service = new TaskCreation(new Context(), authority, store, TimeProvider.System);

        var result = await service.CreateAsync(
            new("Linked Task", "Description", "HIGH", null, ["Item 1"], Request),
            "link-key-1",
            default);

        Assert.NotNull(result);
        Assert.Equal("Linked Task", result.Title);
        Assert.Equal("DRAFT", result.Status);
        Assert.Equal(Request, result.RequestId);
        Assert.True(store.Committed);
        Assert.NotNull(store.CreatedTask);
        Assert.Equal(Request, store.CreatedTask!.RequestId);
        Assert.Equal(Tenant, store.CreatedTask!.TenantId);
        Assert.Equal(User, store.CreatedTask!.CreatorId);
    }

    [Fact]
    public async Task Creating_task_with_invalid_or_inactive_request_throws_validation_error()
    {
        var store = new RequestAwareStore(isValidRequest: false);
        var authority = new GrantAuthorizer();
        var service = new TaskCreation(new Context(), authority, store, TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(
            new("Invalid Link Task", "Description", "MEDIUM", null, null, Request),
            "link-key-2",
            default));

        Assert.Equal(FaultKind.Validation, ex.Kind);
        Assert.Equal("REQUEST.INVALID_STATE", ex.Code);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Idempotency_replay_preserves_request_link()
    {
        var store = new RequestAwareStore(isValidRequest: true)
        {
            ReplayToReturn = new StoredTaskCreation(
                TaskCreationRules.Hash(System.Text.Json.JsonSerializer.Serialize(new
                {
                    version = 1,
                    title = "Existing Linked Task",
                    description = (string?)null,
                    priority = "MEDIUM",
                    deadline = (string?)null,
                    checklist = Array.Empty<string>(),
                    requestId = (Guid?)Request
                })),
                new(Guid.NewGuid(), "Existing Linked Task", "DRAFT", DateTimeOffset.UtcNow, Request))
        };
        var authority = new GrantAuthorizer();
        var service = new TaskCreation(new Context(), authority, store, TimeProvider.System);

        var result = await service.CreateAsync(
            new("Existing Linked Task", null, "MEDIUM", null, null, Request),
            "replay-key",
            default);

        Assert.NotNull(result);
        Assert.Equal("Existing Linked Task", result.Title);
        Assert.Equal(Request, result.RequestId);
        Assert.False(store.Committed); // Returned from replay cache without re-committing
    }

    private sealed class Context : ITenantContext
    {
        public Guid? UserId => User;
        public Guid? TenantId => Tenant;
    }

    private sealed class GrantAuthorizer : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RequestAwareStore(bool isValidRequest) : ITaskCreationStore, ITaskCreationTransaction
    {
        public bool Opened, Disposed, Committed;
        public WorkTask? CreatedTask;
        public StoredTaskCreation? ReplayToReturn;

        public Task<ITaskCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
        {
            Opened = true;
            return Task.FromResult<ITaskCreationTransaction>(this);
        }

        public Task<StoredTaskCreation?> FindReplayAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ReplayToReturn);

        public Task<bool> ValidateRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
            Task.FromResult(isValidRequest && requestId == Request);

        public Task CommitAsync(WorkTask task, IReadOnlyList<TaskChecklistItem> checklist, AuditLog audit, CancellationToken cancellationToken)
        {
            Committed = true;
            CreatedTask = task;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
