using BizFlow.Application.Common;
using BizFlow.Application.Dashboard;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Dashboard;

public sealed class MyWorkDashboardQueryTests
{
    [Fact]
    public async Task An_unauthenticated_or_ungranted_caller_reads_nothing_and_is_audited()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var context = new FakeContext(user, tenant);
        var valid = DashboardFixtures.Snapshot(user, tenant, DashboardFixtures.EmployeeReader);

        foreach (var snapshot in new[] { null, valid with { Grants = [] }, valid with { IsUserActive = false } })
        {
            var audit = new FakeAudit();
            var reader = new FakeMyWorkReader();
            var query = new MyWorkDashboardQuery(context, new FakeSnapshots(snapshot), audit, reader,
                new FixedClock(DashboardFixtures.Now));

            await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(), default));
            Assert.Equal(0, reader.Calls);
            Assert.Equal(1, audit.Calls);
            Assert.Equal("dashboard.my-work.read", audit.Operation);
        }
    }

    [Fact]
    public async Task The_subject_is_always_the_caller_and_the_reader_receives_their_own_scope()
    {
        var employee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var reader = new FakeMyWorkReader();

        await Query(employee, tenant, reader).GetAsync(new(), default);

        Assert.Equal(1, reader.Calls);
        Assert.Equal(employee, reader.Scope!.UserId);
        Assert.Equal(tenant, reader.Scope.TenantId);
        Assert.False(reader.Scope.Tenant);
        Assert.False(reader.Scope.Managed);
    }

    [Fact]
    public async Task Paging_and_status_filters_are_validated_before_the_reader_runs()
    {
        var reader = new FakeMyWorkReader();
        var query = Query(Guid.NewGuid(), Guid.NewGuid(), reader);

        await query.GetAsync(new(Status: "  IN_PROGRESS  "), default);
        Assert.Equal("IN_PROGRESS", reader.Filter!.Status);

        foreach (var filter in new MyWorkFilter[] { new(Page: 0), new(PageSize: 0), new(PageSize: 101), new(Status: "InProgress") })
        {
            var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(filter, default));
            Assert.Equal(FaultKind.Validation, fault.Kind);
        }
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Personal_counters_are_derived_from_the_callers_own_work_items()
    {
        var employee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var slice = new MyWorkSlice(
            Items:
            [
                new MyWorkItemData(DashboardFixtures.WorkItem(
                    DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Critical, user: employee), true), projectId, "Dự án A"),
                new MyWorkItemData(DashboardFixtures.WorkItem(
                    DashboardFixtures.Progress(TaskState.InProgress, TaskPriority.Low, user: employee, overdue: true), true), projectId, "Dự án A"),
                new MyWorkItemData(DashboardFixtures.WorkItem(
                    DashboardFixtures.Progress(TaskState.Cancelled, TaskPriority.Critical, user: employee), true), null, null)
            ],
            Total: 3,
            Projects: [new ProjectSummaryData(DashboardFixtures.Header(projectId),
                [DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Medium, user: employee)])],
            PendingDepartmentClaims: 2);

        var view = await Query(employee, tenant, new FakeMyWorkReader(slice)).GetAsync(new(), default);

        Assert.Equal(3, view.Total);
        Assert.Equal(3, view.Items.Count);
        // Critical(5) at 100 with Low(1) at 50; the cancelled item leaves the denominator.
        Assert.Equal(92, view.MyProgressPercent);
        // Only the InProgress item is active: Completed is finished and Cancelled is dropped.
        Assert.Equal(1, view.ActiveWorkItems);
        Assert.Equal(1, view.CompletedWorkItems);
        Assert.Equal(1, view.OverdueWorkItems);
        Assert.Equal(2, view.PendingDepartmentClaims);
        Assert.Equal(1, view.ByStatus["COMPLETED"]);
        Assert.Equal(1, view.ByStatus["IN_PROGRESS"]);
        Assert.Equal(1, view.ByStatus["CANCELLED"]);
        Assert.Equal("Dự án A", view.Items[0].ProjectTitle);
        Assert.Equal(100, Assert.Single(view.Projects).ProgressPercent);
        Assert.Equal(DashboardFixtures.Now, view.GeneratedAt);
    }

    [Fact]
    public async Task A_caller_with_no_work_at_all_reports_zeroes_instead_of_failing()
    {
        var view = await Query(Guid.NewGuid(), Guid.NewGuid(), new FakeMyWorkReader()).GetAsync(new(), default);

        Assert.Empty(view.Items);
        Assert.Equal(0, view.MyProgressPercent);
        Assert.Equal(0, view.ActiveWorkItems);
        Assert.Equal(0, view.OverdueWorkItems);
        Assert.Empty(view.ByStatus);
        Assert.Empty(view.Projects);
    }

    private static MyWorkDashboardQuery Query(Guid user, Guid tenant, FakeMyWorkReader reader) =>
        new(new FakeContext(user, tenant),
            new FakeSnapshots(DashboardFixtures.Snapshot(user, tenant, DashboardFixtures.EmployeeReader)),
            new FakeAudit(), reader, new FixedClock(DashboardFixtures.Now));
}
