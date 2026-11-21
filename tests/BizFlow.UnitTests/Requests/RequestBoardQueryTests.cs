using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Security;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestBoardQueryTests
{
    [Fact]
    public async Task Missing_mismatched_or_inactive_live_identity_is_audited_before_any_lane_is_read()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var valid = new AccessSnapshot(context.UserId!.Value, context.TenantId, true, true,
            [new(RequestReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        foreach (var snapshot in new[] { null, valid with { UserId = Guid.NewGuid() }, valid with { TenantId = Guid.NewGuid() },
            valid with { IsUserActive = false }, valid with { IsTenantActive = false }, valid with { Grants = [] } })
        {
            var audit = new Audit(); var reader = new Reader();
            var query = new RequestBoardQuery(context, new Provider(snapshot), audit, reader);
            await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(), default));
            Assert.Equal(0, reader.Calls); Assert.Equal(1, audit.Calls);
            Assert.Equal(context.UserId, audit.User); Assert.Equal(context.TenantId, audit.Tenant);
        }
    }

    [Fact]
    public async Task Normalization_and_validation_happen_before_reader_execution()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid()); var reader = new Reader();
        var query = new RequestBoardQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(RequestReadPolicy.OwnPermission, PermissionScope.Self)], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), reader);

        await query.GetAsync(new(Search: " ho so ", Priority: " HIGH "), default);
        Assert.Equal(new RequestBoardFilter(Search: "ho so", Priority: "HIGH"), reader.Filter);

        foreach (var filter in new[]
        {
            new RequestBoardFilter(ColumnSize: 0),
            new(ColumnSize: RequestBoardQuery.MaxColumnSize + 1),
            new(Priority: "Highest"),
            new(Search: new string('x', RequestBoardQuery.MaxSearchLength + 1)),
            new(ServiceId: Guid.Empty),
            new(CategoryId: Guid.Empty),
            new(RequesterId: Guid.Empty)
        })
        {
            var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(filter, default));
            Assert.Equal(FaultKind.Validation, fault.Kind);
        }
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task The_resolved_read_scope_is_handed_to_storage_untouched_and_never_widened()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var department = Guid.NewGuid(); var reader = new Reader();
        var query = new RequestBoardQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(RequestReadPolicy.ManagedPermission, PermissionScope.Department)],
            new HashSet<Guid> { department }, new HashSet<Guid> { department })), new Audit(), reader);

        var view = await query.GetAsync(new(ColumnSize: 7), default);

        Assert.Equal(7, view.ColumnSize);
        Assert.Equal(context.TenantId, reader.Scope!.TenantId);
        Assert.Equal(context.UserId, reader.Scope.UserId);
        Assert.True(reader.Scope.Managed);
        Assert.False(reader.Scope.Tenant);
        Assert.False(reader.Scope.Own);
    }

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;

    private sealed class Provider(AccessSnapshot? snapshot) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class Reader : IRequestBoardReader
    {
        public int Calls { get; private set; }
        public RequestBoardFilter? Filter { get; private set; }
        public RequestReadScope? Scope { get; private set; }
        public Task<IReadOnlyList<RequestBoardColumn>> ReadAsync(RequestReadScope scope, RequestBoardFilter filter, CancellationToken cancellationToken)
        {
            Calls++; Filter = filter; Scope = scope;
            return Task.FromResult<IReadOnlyList<RequestBoardColumn>>(
                [.. Enum.GetValues<RequestState>().Select(state => new RequestBoardColumn(RequestListCodes.State(state), [], 0))]);
        }
    }

    private sealed class Audit : ISecurityAuditWriter
    {
        public int Calls { get; private set; }
        public Guid? User { get; private set; }
        public Guid? Tenant { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        {
            Calls++; User = userId; Tenant = tenantId; return Task.CompletedTask;
        }
    }
}
