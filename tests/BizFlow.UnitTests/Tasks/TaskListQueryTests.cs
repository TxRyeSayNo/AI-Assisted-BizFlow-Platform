using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskListQueryTests
{
    [Fact]
    public async Task Missing_mismatched_or_inactive_live_identity_is_audited_before_any_rows_are_read()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid());
        var valid = new AccessSnapshot(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>());
        foreach (var snapshot in new[] { null, valid with { UserId = Guid.NewGuid() }, valid with { TenantId = Guid.NewGuid() },
            valid with { IsUserActive = false }, valid with { IsTenantActive = false }, valid with { Grants = [] } })
        {
            var audit = new Audit(); var reader = new Reader();
            var query = new TaskListQuery(context, new Provider(snapshot), audit, reader);
            await Assert.ThrowsAsync<ApplicationFault>(() => query.ListAsync(new(), default));
            Assert.Equal(0, reader.Calls); Assert.Equal(1, audit.Calls);
            Assert.Equal(context.UserId, audit.User); Assert.Equal(context.TenantId, audit.Tenant);
        }
    }

    [Fact]
    public async Task Normalization_and_validation_happen_before_reader_execution()
    {
        var context = new Context(Guid.NewGuid(), Guid.NewGuid()); var reader = new Reader();
        var query = new TaskListQuery(context, new Provider(new(context.UserId!.Value, context.TenantId, true, true,
            [new(TaskReadPolicy.OwnPermission, PermissionScope.Self)], new HashSet<Guid>(), new HashSet<Guid>())), new Audit(), reader);
        await query.ListAsync(new(Search: " title ", Status: " IN_PROGRESS ", Priority: " LOW "), default);
        Assert.Equal(new TaskListFilter(Search: "title", Status: "IN_PROGRESS", Priority: "LOW"), reader.Filter);
        foreach (var filter in new[] { new TaskListFilter(Page: 0), new(PageSize: 0), new(Page: int.MaxValue),
            new(Status: "InProgress"), new(Priority: "1"), new(Search: new string('x', 201)) })
        {
            var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.ListAsync(filter, default));
            Assert.Equal(FaultKind.Validation, fault.Kind);
        }
        Assert.Equal(1, reader.Calls);
    }
    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;
    private sealed class Provider(AccessSnapshot? snapshot) : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }
    private sealed class Reader : ITaskListReader
    {
        public int Calls { get; private set; }
        public TaskListFilter? Filter { get; private set; }
        public Task<TaskListPage> ListAsync(TaskReadScope scope, TaskListFilter filter, CancellationToken cancellationToken)
        {
            Calls++; Filter = filter; return Task.FromResult(new TaskListPage([], filter.Page, filter.PageSize, 0));
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
