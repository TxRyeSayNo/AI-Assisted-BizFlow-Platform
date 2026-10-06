using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowNextVersionTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Authorization_precedes_storage_and_is_rechecked_after_the_lock(int deniedCall)
    {
        var store = new Store(); var authority = new Authorizer(deniedCall);
        var service = new WorkflowLibrary(new Context(), authority, new Audit(), store, TimeProvider.System);
        await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateNextVersionAsync(store.Transaction.Workflow.Id, default));
        Assert.Equal(deniedCall, authority.Calls);
        Assert.Equal(deniedCall == 2, store.Opened);
        Assert.Equal(deniedCall == 2, store.Transaction.Disposed);
        Assert.Null(store.Transaction.Version);
    }

    [Fact]
    public async Task Missing_workflow_records_denial_without_creating_a_version()
    {
        var store = new Store { Missing = true }; var audit = new Audit();
        var service = new WorkflowLibrary(new Context(), new Authorizer(), audit, store, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateNextVersionAsync(Guid.NewGuid(), default));
        Assert.Equal(FaultKind.NotFound, failure.Kind); Assert.Equal(AccessDenial.ResourceNotFound, audit.Denial);
        Assert.Null(store.Transaction.Version);
    }

    [Fact]
    public async Task Next_number_and_audit_are_created_by_application_under_the_transaction()
    {
        var store = new Store();
        var service = new WorkflowLibrary(new Context(), new Authorizer(), new Audit(), store, TimeProvider.System);
        var result = await service.CreateNextVersionAsync(store.Transaction.Workflow.Id, default);
        Assert.Equal(8, result.LatestVersionNo); Assert.Equal("DRAFT", result.LatestVersionStatus);
        Assert.True(store.Transaction.Disposed);
        Assert.Equal("{}", store.Transaction.Version!.DefinitionJson); Assert.Null(store.Transaction.Version.PublishedAt);
        Assert.Equal(store.Transaction.Version.Id, store.Transaction.Entry!.ObjectId);
        Assert.Equal(User, store.Transaction.Entry.ActorId); Assert.Equal(Tenant, store.Transaction.Entry.TenantId);
        Assert.Equal("WORKFLOW.VERSION_DRAFT_CREATED", store.Transaction.Entry.Action);
    }

    private sealed class Context : ITenantContext { public Guid? UserId => User; public Guid? TenantId => Tenant; }
    private sealed class Authorizer(int deniedCall = 0) : IResourceAuthorizer
    {
        public int Calls { get; private set; }
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("workflows.configure", permission); Assert.Equal(Tenant, resource.TenantId);
            if (++Calls == deniedCall) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Denied");
            return Task.CompletedTask;
        }
    }
    private sealed class Audit : ISecurityAuditWriter
    {
        public AccessDenial? Denial { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        { Assert.Equal(User, userId); Assert.Equal(Tenant, tenantId); Denial = reason; return Task.CompletedTask; }
    }
    private sealed class Store : IWorkflowLibraryStore
    {
        public Transaction Transaction { get; } = new();
        public bool Missing { get; init; }
        public bool Opened { get; private set; }
        public Task<IWorkflowVersionTransaction?> BeginNextVersionAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken)
        { Assert.Equal(Tenant, tenantId); Opened = true; return Task.FromResult<IWorkflowVersionTransaction?>(Missing ? null : Transaction); }
        public Task<WorkflowPage> ListAsync(Guid tenantId, WorkflowFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkflowVersionView?> ReadVersionAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkflowRow> CreateAsync(WorkflowDefinition workflow, WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Transaction : IWorkflowVersionTransaction
    {
        public WorkflowDefinition Workflow { get; } = WorkflowDefinition.CreateDraft(Tenant, "Existing workflow", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        public int LatestVersionNo => 7;
        public WorkflowVersion? Version { get; private set; }
        public AuditLog? Entry { get; private set; }
        public bool Disposed { get; private set; }
        public Task<WorkflowRow> CommitAsync(WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken)
        { Version = version; Entry = audit; return Task.FromResult(new WorkflowRow(Workflow.Id, Workflow.Name, "TASK", "DRAFT", version.Id, version.VersionNo, "DRAFT")); }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
