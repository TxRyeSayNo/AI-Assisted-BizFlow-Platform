using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskCalendarQueryTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_mismatched_or_inactive_live_identity_is_audited_before_any_window_is_read()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var valid = new AccessSnapshot(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        foreach (var snapshot in new[] { null, valid with { UserId = Guid.NewGuid() }, valid with { TenantId = Guid.NewGuid() },
            valid with { IsUserActive = false }, valid with { IsTenantActive = false }, valid with { Grants = [] } })
        {
            var audit = new Audit(); var reader = new Reader();
            var query = new TaskCalendarQuery(context, new Provider(snapshot), audit, reader);
            await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(Anchor, Anchor.AddDays(30)), default));
            Assert.Equal(0, reader.Calls); Assert.Equal(1, audit.Calls);
        }
    }

    [Fact]
    public async Task Normalization_and_window_validation_happen_before_reader_execution()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid()); var reader = new Reader();
        var query = new TaskCalendarQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.OwnPermission, PermissionScope.Self)], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), reader);

        await query.GetAsync(new(Anchor, Anchor.AddDays(30), Status: " IN_PROGRESS ", Priority: " LOW "), default);
        Assert.Equal("IN_PROGRESS", reader.Filter!.Status);
        Assert.Equal("LOW", reader.Filter.Priority);

        foreach (var filter in new[]
        {
            new TaskCalendarFilter(Anchor, Anchor.AddDays(30), Limit: 0),
            new(Anchor, Anchor.AddDays(30), Limit: TaskCalendarQuery.MaxLimit + 1),
            // An inverted window, and one wider than a year, would both scan without bound.
            new(Anchor.AddDays(1), Anchor),
            new(Anchor, Anchor.AddDays(TaskCalendarQuery.MaxWindowDays + 1)),
            new(Anchor, Anchor.AddDays(30), Status: "InProgress"),
            new(Anchor, Anchor.AddDays(30), Priority: "1"),
            new(Anchor, Anchor.AddDays(30), AssignedUserId: Guid.Empty),
            new(Anchor, Anchor.AddDays(30), AssignedDepartmentId: Guid.Empty)
        })
        {
            var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(filter, default));
            Assert.Equal(FaultKind.Validation, fault.Kind);
        }
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task The_window_and_truncation_flag_are_reported_back_to_the_caller()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var reader = new Reader { Truncated = true };
        var query = new TaskCalendarQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), reader);

        var view = await query.GetAsync(new(Anchor, Anchor.AddDays(7), Limit: 25), default);

        Assert.Equal(Anchor, view.From);
        Assert.Equal(Anchor.AddDays(7), view.To);
        Assert.Equal(25, view.Limit);
        Assert.True(view.Truncated);
        Assert.True(reader.Scope!.Tenant);
    }

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;

    private sealed class Provider(AccessSnapshot? snapshot) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class Reader : ITaskCalendarReader
    {
        public int Calls { get; private set; }
        public bool Truncated { get; init; }
        public TaskCalendarFilter? Filter { get; private set; }
        public TaskReadScope? Scope { get; private set; }
        public Task<(IReadOnlyList<TaskListRow> Items, bool Truncated)> ReadAsync(TaskReadScope scope,
            TaskCalendarFilter filter, CancellationToken cancellationToken)
        {
            Calls++; Filter = filter; Scope = scope;
            return Task.FromResult<(IReadOnlyList<TaskListRow>, bool)>(([], Truncated));
        }
    }

    private sealed class Audit : ISecurityAuditWriter
    {
        public int Calls { get; private set; }
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken)
        {
            Calls++; return Task.CompletedTask;
        }
    }
}
