using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskCreationAuthorizationTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), User = Guid.NewGuid();
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Live_authority_is_required_before_storage_and_again_after_database_lock(int deniedCall)
    {
        var store = new Store(); var authority = new Authorizer(deniedCall);
        var service = new TaskCreation(new Context(), authority, store, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(new("Draft"), "attempt", default));
        Assert.Equal(FaultKind.Forbidden, failure.Kind);
        Assert.Equal(deniedCall == 2, store.Opened); Assert.Equal(deniedCall == 2, store.Disposed);
        Assert.False(store.ReplayRead); Assert.False(store.Committed);
    }
    private sealed class Context : ITenantContext { public Guid? UserId => User; public Guid? TenantId => Tenant; }
    private sealed class Authorizer(int deniedCall) : IResourceAuthorizer
    {
        private int calls;
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("tasks.create", permission); Assert.Equal(Tenant, resource.TenantId);
            if (++calls == deniedCall) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Denied");
            return Task.CompletedTask;
        }
    }
    private sealed class Store : ITaskCreationStore, ITaskCreationTransaction
    {
        public bool Opened, Disposed, ReplayRead, Committed;
        public Task<ITaskCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken)
        {
            Assert.Equal(Tenant, tenantId); Assert.Equal(User, actorId); Assert.Equal(TaskCreationRules.KeyHash("attempt"), keyHash);
            Opened = true; return Task.FromResult<ITaskCreationTransaction>(this);
        }
        public Task<StoredTaskCreation?> FindReplayAsync(CancellationToken cancellationToken) { ReplayRead = true; return Task.FromResult<StoredTaskCreation?>(null); }
        public Task CommitAsync(WorkTask task, IReadOnlyList<TaskChecklistItem> checklist, AuditLog audit, CancellationToken cancellationToken) { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
