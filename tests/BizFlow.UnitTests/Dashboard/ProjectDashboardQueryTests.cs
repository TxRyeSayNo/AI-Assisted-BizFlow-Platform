using BizFlow.Application.Common;
using BizFlow.Application.Dashboard;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Dashboard;

public sealed class ProjectDashboardQueryTests
{
    [Fact]
    public async Task Missing_mismatched_or_ungranted_identity_is_audited_before_any_row_is_read()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var context = new FakeContext(user, tenant);
        var valid = DashboardFixtures.Snapshot(user, tenant);

        foreach (var snapshot in new[]
        {
            null,
            valid with { UserId = Guid.NewGuid() },
            valid with { TenantId = Guid.NewGuid() },
            valid with { IsUserActive = false },
            valid with { IsTenantActive = false },
            valid with { Grants = [] },
            valid with { Grants = [new PermissionGrant("reports.manager", PermissionScope.Tenant)] }
        })
        {
            var audit = new FakeAudit();
            var reader = new FakeProjectReader();
            // Built directly: an explicitly absent snapshot must stay absent, not fall back to a valid one.
            var query = new ProjectDashboardQuery(context, new FakeSnapshots(snapshot), audit, reader,
                new FixedClock(DashboardFixtures.Now));

            await Assert.ThrowsAsync<ApplicationFault>(() => query.ListAsync(new(), default));
            Assert.Equal(0, reader.ListCalls);
            Assert.Equal(1, audit.Calls);
            Assert.Equal(user, audit.User);
            Assert.Equal(tenant, audit.Tenant);
            Assert.Equal("dashboard.projects.read", audit.Operation);
        }
    }

    [Fact]
    public async Task A_platform_account_without_a_tenant_cannot_reach_the_project_dashboard()
    {
        var user = Guid.NewGuid();
        var context = new FakeContext(user, null);
        var snapshot = new AccessSnapshot(user, null, true, true,
            [new("platform.audit.read", PermissionScope.Platform)], new HashSet<Guid>(), new HashSet<Guid>());
        var reader = new FakeProjectReader();

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => Query(context, snapshot, reader).ListAsync(new(), default));
        Assert.Equal(FaultKind.Forbidden, fault.Kind);
        Assert.Equal(0, reader.ListCalls);
    }

    [Fact]
    public async Task Filters_are_normalized_and_validated_before_the_reader_runs()
    {
        var reader = new FakeProjectReader();
        var query = Query(reader: reader);

        await query.ListAsync(new(Search: "  nâng cấp  ", Size: " large "), default);
        Assert.Equal("nâng cấp", reader.Filter!.Search);
        Assert.Equal("LARGE", reader.Filter.Size);

        foreach (var filter in new ProjectDashboardFilter[]
        {
            new(Page: 0),
            new(PageSize: 0),
            new(PageSize: 101),
            new(Page: int.MaxValue),
            new(Size: "HUGE"),
            new(Search: new string('x', 201)),
            new(DepartmentId: Guid.Empty)
        })
        {
            var fault = await Assert.ThrowsAsync<ApplicationFault>(() => query.ListAsync(filter, default));
            Assert.Equal(FaultKind.Validation, fault.Kind);
        }
        Assert.Equal(1, reader.ListCalls);
    }

    [Fact]
    public async Task An_unreachable_or_empty_project_identifier_is_reported_as_not_found()
    {
        var reader = new FakeProjectReader(detail: null);
        var query = Query(reader: reader);

        var missing = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(Guid.NewGuid(), default));
        Assert.Equal(FaultKind.NotFound, missing.Kind);
        Assert.Equal(1, reader.GetCalls);

        var empty = await Assert.ThrowsAsync<ApplicationFault>(() => query.GetAsync(Guid.Empty, default));
        Assert.Equal(FaultKind.NotFound, empty.Kind);
        Assert.Equal(1, reader.GetCalls);
    }

    [Fact]
    public async Task A_manager_sees_the_whole_participant_split_of_a_project_in_scope()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var department = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var detail = Detail(manager, alice);
        var reader = new FakeProjectReader(detail: detail);
        var query = Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { department }),
            reader);

        var view = await query.GetAsync(detail.Header.ProjectId, default);

        Assert.True(view.CanSeeAllParticipants);
        Assert.Equal(2, view.Participants.Count);
        Assert.Equal(100m, view.Participants.Sum(x => x.ResponsibilityPercent));
        Assert.Equal(3, view.WorkItems.Count);
        Assert.Equal(0, view.HiddenWorkItemCount);
        Assert.Contains(view.Participants, x => x.IsSelf && x.UserId == manager);
    }

    [Fact]
    public async Task An_employee_sees_the_shared_project_picture_but_only_their_own_rows()
    {
        var employee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        // The employee holds one Low work item; the other person holds a Critical one plus an
        // unclaimed department item. Only the employee's own row may be disclosed.
        var projectId = Guid.NewGuid();
        var mine = DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Low, user: employee);
        var theirs = DashboardFixtures.Progress(TaskState.Draft, TaskPriority.Critical, user: other);
        var detail = new ProjectData(DashboardFixtures.Header(projectId, other),
        [
            DashboardFixtures.WorkItem(mine, visible: true, title: "Việc của tôi", assigneeName: "Tôi"),
            DashboardFixtures.WorkItem(theirs, visible: false, title: "Việc người khác", assigneeName: "Người khác")
        ]);
        var query = Query(new FakeContext(employee, tenant),
            DashboardFixtures.Snapshot(employee, tenant, DashboardFixtures.EmployeeReader),
            new FakeProjectReader(detail: detail));

        var view = await query.GetAsync(projectId, default);

        Assert.False(view.CanSeeAllParticipants);
        var self = Assert.Single(view.Participants);
        Assert.Equal(employee, self.UserId);
        Assert.True(self.IsSelf);
        // Low(1) of Low(1)+Critical(5) = 16.7% of the whole project, not 100% of what they can see.
        Assert.Equal(16.7m, self.ResponsibilityPercent);
        Assert.Equal(16.7m, view.MyResponsibilityPercent);
        Assert.Equal(100m, view.MyContributionPercent);

        // Row-level disclosure is narrowed, while the shared project totals stay truthful.
        Assert.Single(view.WorkItems);
        Assert.Equal("Việc của tôi", view.WorkItems[0].Title);
        Assert.Equal(1, view.HiddenWorkItemCount);
        Assert.Equal(1, view.HiddenParticipantCount);
        Assert.Equal(2, view.Project.WorkItemCount);
        Assert.Equal(2, view.Project.ParticipantCount);
        Assert.Equal(17, view.Project.ProgressPercent);
        Assert.DoesNotContain(view.Participants, x => x.UserId == other);
    }

    [Fact]
    public async Task A_manager_is_not_told_who_holds_work_outside_their_own_scope()
    {
        var manager = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var department = Guid.NewGuid();
        var mySubordinate = Guid.NewGuid();
        var otherTeamMember = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var detail = new ProjectData(DashboardFixtures.Header(projectId, manager),
        [
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.InProgress, TaskPriority.Medium, user: mySubordinate),
                visible: true, title: "Việc cấp dưới của tôi", assigneeName: "Cấp dưới"),
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Medium, user: otherTeamMember),
                visible: false, title: "Việc phòng khác", assigneeName: "Người phòng khác")
        ]);

        var view = await Query(new FakeContext(manager, tenant),
            DashboardFixtures.Snapshot(manager, tenant, DashboardFixtures.ManagerReader, new HashSet<Guid> { department }),
            new FakeProjectReader(detail: detail)).GetAsync(projectId, default);

        Assert.True(view.CanSeeAllParticipants);
        var disclosed = Assert.Single(view.Participants);
        Assert.Equal(mySubordinate, disclosed.UserId);
        Assert.Equal(1, view.HiddenParticipantCount);
        Assert.DoesNotContain(view.Participants, x => x.UserId == otherTeamMember);
        // The split is still a share of the whole project, not of the visible slice only.
        Assert.Equal(50m, disclosed.ResponsibilityPercent);
        Assert.Equal(1, view.HiddenWorkItemCount);
        Assert.Equal(2, view.Project.WorkItemCount);
        Assert.Equal(75, view.Project.ProgressPercent);
    }

    [Fact]
    public async Task A_company_wide_reader_sees_every_participant_of_the_project()
    {
        var admin = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var detail = new ProjectData(DashboardFixtures.Header(projectId),
        [
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.InProgress, TaskPriority.Medium, user: one),
                visible: true, assigneeName: "Một"),
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.Draft, TaskPriority.Medium, user: two),
                visible: true, assigneeName: "Hai")
        ]);

        var view = await Query(new FakeContext(admin, tenant),
            DashboardFixtures.Snapshot(admin, tenant, DashboardFixtures.TenantReader),
            new FakeProjectReader(detail: detail)).GetAsync(projectId, default);

        Assert.Equal(2, view.Participants.Count);
        Assert.Equal(0, view.HiddenParticipantCount);
        Assert.Equal(0, view.HiddenWorkItemCount);
    }

    [Fact]
    public async Task A_participant_outside_the_split_reports_a_zero_share_without_failing()
    {
        var viewer = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var unassigned = DashboardFixtures.Progress(TaskState.Draft, TaskPriority.Medium);
        var detail = new ProjectData(DashboardFixtures.Header(projectId),
            [DashboardFixtures.WorkItem(unassigned, visible: true)]);

        var view = await Query(new FakeContext(viewer, tenant),
            DashboardFixtures.Snapshot(viewer, tenant, DashboardFixtures.EmployeeReader),
            new FakeProjectReader(detail: detail)).GetAsync(projectId, default);

        Assert.Empty(view.Participants);
        Assert.Equal(0m, view.MyResponsibilityPercent);
        Assert.Equal(0m, view.MyContributionPercent);
        Assert.Equal(1, view.Project.UnassignedWorkItemCount);
    }

    [Fact]
    public async Task The_generated_timestamp_and_paging_echo_the_server_clock_and_request()
    {
        var reader = new FakeProjectReader(new ProjectListSlice([], 42));
        var page = await Query(reader: reader).ListAsync(new(Page: 2, PageSize: 10), default);

        Assert.Equal(DashboardFixtures.Now, page.GeneratedAt);
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(42, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task A_legacy_project_without_work_items_reports_its_own_state_as_progress()
    {
        var header = DashboardFixtures.Header(Guid.NewGuid()) with { Status = TaskState.Submitted };
        var reader = new FakeProjectReader(new ProjectListSlice([new ProjectSummaryData(header, [])], 1));

        var page = await Query(reader: reader).ListAsync(new(), default);

        var summary = Assert.Single(page.Items);
        Assert.Equal(80, summary.ProgressPercent);
        Assert.Equal(0, summary.WorkItemCount);
        Assert.Equal("SMALL", summary.SizeCategory);
        Assert.Equal("SUBMITTED", summary.Status);
        Assert.Equal("MEDIUM", summary.Priority);
    }

    private static ProjectData Detail(Guid manager, Guid alice)
    {
        var projectId = Guid.NewGuid();
        return new ProjectData(DashboardFixtures.Header(projectId, manager),
        [
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.Completed, TaskPriority.Medium, user: manager),
                visible: true, title: "Lập kế hoạch", assigneeName: "Quản lý"),
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.InProgress, TaskPriority.High, user: alice),
                visible: true, title: "Triển khai", assigneeName: "Alice"),
            DashboardFixtures.WorkItem(DashboardFixtures.Progress(TaskState.Draft, TaskPriority.Low, user: alice),
                visible: true, title: "Kiểm thử", assigneeName: "Alice")
        ]);
    }

    private static ProjectDashboardQuery Query(FakeContext? context = null, AccessSnapshot? snapshot = null,
        FakeProjectReader? reader = null, FakeAudit? audit = null)
    {
        var user = context?.UserId ?? Guid.NewGuid();
        var tenant = context?.TenantId ?? Guid.NewGuid();
        var resolved = context ?? new FakeContext(user, tenant);
        return new ProjectDashboardQuery(
            resolved,
            new FakeSnapshots(snapshot ?? DashboardFixtures.Snapshot(user, tenant)),
            audit ?? new FakeAudit(),
            reader ?? new FakeProjectReader(),
            new FixedClock(DashboardFixtures.Now));
    }
}
