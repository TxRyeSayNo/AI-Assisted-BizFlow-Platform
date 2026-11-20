using BizFlow.Application.Common;
using BizFlow.Application.Dashboard;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Dashboard;

public sealed class TeamDashboardQueryTests
{
    [Fact]
    public async Task An_employee_with_only_personal_task_read_is_refused_and_audited()
    {
        var employee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var audit = new FakeAudit();
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(employee, tenant),
            DashboardFixtures.Snapshot(employee, tenant, DashboardFixtures.EmployeeReader), reader, audit);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(), default));

        Assert.Equal(FaultKind.Forbidden, fault.Kind);
        Assert.Equal("ACCESS.DENIED", fault.Code);
        Assert.Equal(0, reader.Calls);
        Assert.Equal(1, audit.Calls);
        Assert.Equal(AccessDenial.PermissionDenied, audit.Reason);
        Assert.Equal("dashboard.team.read", audit.Operation);
    }

    [Fact]
    public async Task A_manager_cannot_read_another_managers_department_by_passing_its_identifier()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var someoneElses = Guid.NewGuid();
        var audit = new FakeAudit();
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { mine }),
            reader, audit);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(someoneElses), default));

        Assert.Equal(FaultKind.Forbidden, fault.Kind);
        Assert.Equal("TASK.TARGET_OUT_OF_SCOPE", fault.Code);
        Assert.Equal(0, reader.Calls);
        Assert.Equal(1, audit.Calls);
        Assert.Equal(AccessDenial.ManagementScopeDenied, audit.Reason);
    }

    [Fact]
    public async Task A_manager_without_a_filter_reads_exactly_their_expanded_management_scope()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { root, child }),
            reader);

        var view = await query.GetAsync(new(), default);

        Assert.Equal(1, reader.Calls);
        Assert.False(reader.DepartmentIdsWereNull);
        Assert.Equal(2, reader.DepartmentIds!.Count);
        Assert.Contains(root, reader.DepartmentIds);
        Assert.Contains(child, reader.DepartmentIds);
        Assert.Equal(tenant, reader.TenantId);
        Assert.Equal(TeamDashboardQuery.MemberLimit, reader.MemberLimit);
        Assert.False(view.TenantWide);
        Assert.Equal(2, view.ScopeDepartmentIds.Count);
    }

    [Fact]
    public async Task A_manager_may_narrow_to_one_department_inside_their_scope()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var alsoMine = Guid.NewGuid();
        var reader = new FakeTeamReader(new TeamSlice(
            [new TeamDepartmentData(mine, "Phòng của tôi", null, 0)], [], false));
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader,
                new HashSet<Guid> { mine, alsoMine }), reader);

        await query.GetAsync(new(mine), default);

        Assert.Equal(1, reader.Calls);
        Assert.Equal(mine, Assert.Single(reader.DepartmentIds!));
    }

    [Fact]
    public async Task A_company_wide_reader_sees_every_department_and_may_pick_any_of_them()
    {
        var admin = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var anyDepartment = Guid.NewGuid();

        var unfiltered = new FakeTeamReader();
        var view = await Query(new FakeContext(admin, tenant),
            DashboardFixtures.Snapshot(admin, tenant, DashboardFixtures.TenantReader), unfiltered).GetAsync(new(), default);
        Assert.True(unfiltered.DepartmentIdsWereNull);
        Assert.True(view.TenantWide);
        Assert.Empty(view.ScopeDepartmentIds);

        // A tenant-wide grant holds no management scope rows, so a department filter must still work.
        var filtered = new FakeTeamReader(new TeamSlice(
            [new TeamDepartmentData(anyDepartment, "Phòng bất kỳ", null, 0)], [], false));
        await Query(new FakeContext(admin, tenant),
            DashboardFixtures.Snapshot(admin, tenant, DashboardFixtures.TenantReader), filtered)
            .GetAsync(new(anyDepartment), default);
        Assert.Equal(anyDepartment, Assert.Single(filtered.DepartmentIds!));
    }

    [Fact]
    public async Task A_department_that_does_not_exist_in_this_tenant_is_reported_as_missing()
    {
        var admin = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        // A company-wide reader is not scope-checked, so an unknown or foreign department must
        // fail closed instead of returning a convincing empty team.
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(admin, tenant),
            DashboardFixtures.Snapshot(admin, tenant, DashboardFixtures.TenantReader), reader);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(Guid.NewGuid()), default));

        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task A_manager_with_an_empty_scope_reads_nothing_instead_of_failing()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader), reader);

        var view = await query.GetAsync(new(), default);

        Assert.Empty(view.Members);
        Assert.Empty(view.Departments);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlySet<Guid>>(reader.DepartmentIds));
    }

    [Fact]
    public async Task An_empty_department_identifier_is_a_validation_error()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var reader = new FakeTeamReader();
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.TenantReader), reader);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(new(Guid.Empty), default));
        Assert.Equal(FaultKind.Validation, fault.Kind);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Member_load_progress_and_responsibility_share_are_computed_from_live_work()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var department = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var slice = new TeamSlice(
            Departments: [new TeamDepartmentData(department, "Phòng Kỹ thuật", null, 2)],
            Members:
            [
                new TeamMemberData(alice, "Alice", "EMP001", department, "Phòng Kỹ thuật", "ACTIVE",
                [
                    DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Critical, user: alice),
                    DashboardFixtures.Progress(TaskState.InProgress, TaskPriority.Low, user: alice, overdue: true),
                    DashboardFixtures.Progress(TaskState.Cancelled, TaskPriority.Critical, user: alice)
                ]),
                new TeamMemberData(bob, "Bob", "EMP002", department, "Phòng Kỹ thuật", "ACTIVE",
                [
                    DashboardFixtures.Progress(TaskState.Draft, TaskPriority.Medium, user: bob)
                ])
            ],
            Truncated: false);
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { department }),
            new FakeTeamReader(slice));

        var view = await query.GetAsync(new(), default);

        var aliceView = Assert.Single(view.Members, x => x.UserId == alice);
        Assert.Equal(3, aliceView.TotalAssigned);
        // Only the InProgress item is active: Completed is finished and Cancelled is dropped.
        Assert.Equal(1, aliceView.ActiveWorkItems);
        Assert.Equal(1, aliceView.CompletedWorkItems);
        Assert.Equal(1, aliceView.OverdueWorkItems);
        // Critical(5) at 100 and Low(1) at 50; the cancelled Critical item is excluded.
        Assert.Equal(92, aliceView.AvgProgressPercent);
        // Alice holds weight 6 of 8 across the scope.
        Assert.Equal(75m, aliceView.ResponsibilitySharePercent);
        Assert.Equal(25m, Assert.Single(view.Members, x => x.UserId == bob).ResponsibilitySharePercent);
        Assert.Equal(100m, view.Members.Sum(x => x.ResponsibilitySharePercent));

        var departmentView = Assert.Single(view.Departments);
        Assert.Equal(2, departmentView.MemberCount);
        Assert.Equal(1, departmentView.ActiveWorkItems);
        Assert.Equal(1, departmentView.OverdueWorkItems);
        Assert.Equal(DashboardFixtures.Now, view.GeneratedAt);
    }

    [Fact]
    public async Task A_member_holding_no_live_work_reports_zero_without_dividing_by_zero()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var department = Guid.NewGuid();
        var idle = Guid.NewGuid();
        var slice = new TeamSlice(
            [new TeamDepartmentData(department, "Phòng Hành chính", null, 1)],
            [new TeamMemberData(idle, "Chưa có việc", "EMP009", department, "Phòng Hành chính", "ACTIVE", [])],
            Truncated: false);

        var view = await Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { department }),
            new FakeTeamReader(slice)).GetAsync(new(), default);

        var member = Assert.Single(view.Members);
        Assert.Equal(0, member.AvgProgressPercent);
        Assert.Equal(0m, member.ResponsibilitySharePercent);
        Assert.Equal(0, Assert.Single(view.Departments).AvgProgressPercent);
    }

    private static TeamDashboardQuery Query(FakeContext context, AccessSnapshot snapshot,
        FakeTeamReader reader, FakeAudit? audit = null) =>
        new(context, new FakeSnapshots(snapshot), audit ?? new FakeAudit(), reader,
            new FixedClock(DashboardFixtures.Now));
}
