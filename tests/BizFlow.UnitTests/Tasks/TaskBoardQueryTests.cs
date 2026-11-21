using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskBoardQueryTests
{
    [Fact]
    public async Task Missing_mismatched_or_inactive_live_identity_is_audited_before_any_lane_is_read()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var valid = new AccessSnapshot(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        foreach (var snapshot in new[] { null, valid with { UserId = Guid.NewGuid() }, valid with { TenantId = Guid.NewGuid() },
            valid with { IsUserActive = false }, valid with { IsTenantActive = false }, valid with { Grants = [] } })
        {
            var audit = new Audit(); var reader = new Reader();
            var query = new TaskBoardQuery(context, new Provider(snapshot), audit, reader);
            await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(), default));
            Assert.Equal(0, reader.Calls); Assert.Equal(1, audit.Calls);
            Assert.Equal(context.UserId, audit.User); Assert.Equal(context.TenantId, audit.Tenant);
        }
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_told_to_authenticate_and_a_permitted_one_is_refused()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var anonymous = new TaskBoardQuery(context, new Provider(null), new Audit(), new Reader());
        Assert.Equal(FaultKind.Unauthenticated,
            (await Assert.ThrowsAsync<ApplicationFault>(() => anonymous.GetAsync(new(), default))).Kind);

        var withoutGrants = new TaskBoardQuery(context, new Provider(new(context.UserId!.Value, context.TenantId,
            true, true, [], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), new Reader());
        Assert.Equal(FaultKind.Forbidden,
            (await Assert.ThrowsAsync<ApplicationFault>(() => withoutGrants.GetAsync(new(), default))).Kind);
    }

    [Fact]
    public async Task Normalization_and_validation_happen_before_reader_execution()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid()); var reader = new Reader();
        var query = new TaskBoardQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.OwnPermission, PermissionScope.Self)], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), reader);

        await query.GetAsync(new(Search: " title ", Priority: " LOW "), default);
        Assert.Equal(new TaskBoardFilter(Search: "title", Priority: "LOW"), reader.Filter);

        var deadline = DateTimeOffset.UtcNow;
        foreach (var filter in new[]
        {
            new TaskBoardFilter(ColumnSize: 0),
            new(ColumnSize: TaskBoardQuery.MaxColumnSize + 1),
            new(Priority: "1"),
            new(Search: new string('x', TaskBoardQuery.MaxSearchLength + 1)),
            new(AssignedUserId: Guid.Empty),
            new(AssignedDepartmentId: Guid.Empty),
            new(RequestId: Guid.Empty),
            // An inverted deadline window would otherwise reach storage and silently return nothing.
            new(DeadlineFrom: deadline.AddDays(1), DeadlineTo: deadline)
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
        var query = new TaskBoardQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.ManagedPermission, PermissionScope.Department)],
            new HashSet<Guid> { department }, new HashSet<Guid> { department })), new Audit(), reader);

        var view = await query.GetAsync(new(ColumnSize: 10), default);

        Assert.Equal(10, view.ColumnSize);
        Assert.Equal(context.TenantId, reader.Scope!.TenantId);
        Assert.Equal(context.UserId, reader.Scope.UserId);
        Assert.True(reader.Scope.Managed);
        Assert.False(reader.Scope.Tenant);
        Assert.False(reader.Scope.Own);
        Assert.False(reader.Scope.Assigned);
    }

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;

    private sealed class Provider(AccessSnapshot? snapshot) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class Reader : ITaskBoardReader
    {
        public int Calls { get; private set; }
        public TaskBoardFilter? Filter { get; private set; }
        public TaskReadScope? Scope { get; private set; }
        public Task<IReadOnlyList<TaskBoardColumn>> ReadAsync(TaskReadScope scope, TaskBoardFilter filter, CancellationToken cancellationToken)
        {
            Calls++; Filter = filter; Scope = scope;
            return Task.FromResult<IReadOnlyList<TaskBoardColumn>>(
                [.. Enum.GetValues<TaskState>().Select(state => new TaskBoardColumn(TaskListCodes.State(state), [], 0))]);
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
