using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Sla;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;

namespace BizFlow.UnitTests;

public sealed class SlaVersionCreationTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Authorization_precedes_storage_and_is_rechecked_after_configuration_and_recipient_locks(int deniedCall)
    {
        var store = new Store(); var authority = new Authorizer(deniedCall);
        var service = new SlaVersionCreation(new Context(), authority, new Audit(), store, TimeProvider.System);
        await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(store.Transaction.Profile.Id, new(60, 45, store.Transaction.Calendar.Id), default));
        Assert.Equal(deniedCall, authority.Calls); Assert.Equal(deniedCall > 1, store.Opened);
        Assert.Equal(deniedCall > 1, store.Transaction.Disposed); Assert.Equal(deniedCall == 3, store.Transaction.RecipientsLocked);
        Assert.Null(store.Transaction.Version);
    }
    [Fact]
    public async Task Missing_reference_creates_caller_scoped_denial_not_business_audit()
    {
        var store = new Store { Missing = true }; var audit = new Audit();
        var service = new SlaVersionCreation(new Context(), new Authorizer(), audit, store, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(Guid.NewGuid(), new(60, 45, Guid.NewGuid()), default));
        Assert.Equal(FaultKind.NotFound, failure.Kind); Assert.Equal(AccessDenial.ResourceNotFound, audit.Denial);
        Assert.Null(store.Transaction.Version);
    }
    [Fact]
    public async Task Application_assigns_next_number_and_commits_matching_actor_audit()
    {
        var store = new Store(); var service = new SlaVersionCreation(new Context(), new Authorizer(), new Audit(), store, TimeProvider.System);
        var result = await service.CreateAsync(store.Transaction.Profile.Id, new(60, 45, store.Transaction.Calendar.Id), default);
        Assert.Equal(8, result.VersionNo); Assert.Equal(60, result.TargetMinutes); Assert.Equal(45, result.WarningMinutes);
        Assert.True(store.Transaction.Disposed); Assert.True(store.Transaction.RecipientsLocked);
        Assert.Equal(result.SlaVersionId, store.Transaction.Entry!.ObjectId); Assert.Equal(User, store.Transaction.Entry.ActorId);
        Assert.Equal(Tenant, store.Transaction.Entry.TenantId); Assert.Equal("SLA.VERSION_CREATED", store.Transaction.Entry.Action);
        Assert.Equal(SlaProfileStatus.Draft, store.Transaction.Profile.Status);
    }
    private sealed class Context : ITenantContext { public Guid? UserId => User; public Guid? TenantId => Tenant; }
    private sealed class Authorizer(int deniedCall = 0) : IResourceAuthorizer
    {
        public int Calls { get; private set; }
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("sla.configure", permission); Assert.Equal(Tenant, resource.TenantId);
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
    private sealed class Store : ISlaVersionStore
    {
        public Transaction Transaction { get; } = new();
        public bool Missing { get; init; }
        public bool Opened { get; private set; }
        public Task<ISlaVersionTransaction?> BeginAsync(Guid tenantId, Guid profileId, Guid calendarId, CancellationToken cancellationToken)
        { Assert.Equal(Tenant, tenantId); Opened = true; return Task.FromResult<ISlaVersionTransaction?>(Missing ? null : Transaction); }
    }
    private sealed class Transaction : ISlaVersionTransaction
    {
        public SlaProfile Profile { get; } = SlaProfile.CreateDraft(Tenant, "Response");
        public BusinessCalendar Calendar { get; } = BusinessCalendar.Create(Tenant, "UTC", """{"monday":[{"start":"08:00","end":"17:30"}]}""");
        public int LatestVersionNo => 7;
        public bool Disposed { get; private set; }
        public bool RecipientsLocked { get; private set; }
        public SlaVersion? Version { get; private set; }
        public AuditLog? Entry { get; private set; }
        public Task<bool> LockActiveRecipientsAsync(IReadOnlyList<Guid> recipients, CancellationToken cancellationToken)
        { RecipientsLocked = true; return Task.FromResult(true); }
        public Task CommitAsync(SlaVersion version, AuditLog audit, CancellationToken cancellationToken)
        { Version = version; Entry = audit; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
