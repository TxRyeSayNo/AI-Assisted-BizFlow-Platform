using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Dashboard;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for the project, personal and team dashboards: the real SQL translation
/// against PostgreSQL plus the authorization boundaries the feature promises.
/// </summary>
[Collection("Postgres")]
public sealed class ProjectDashboardApiTests(PostgresFixture database)
{
    private const string Password = "Project-dashboard-test!5518";
    private const string Projects = "/api/v1/dashboard/projects";
    private const string MyWork = "/api/v1/dashboard/my-work";
    private const string Team = "/api/v1/dashboard/team";

    [Fact]
    public async Task A_company_wide_reader_sees_every_project_and_never_a_task_without_work_items()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var page = (await client.GetFromJsonAsync<ProjectDashboardPage>(Projects))!;

        Assert.Equal(new[] { "Dự án Alpha", "Dự án Beta" }, page.Items.Select(x => x.Title).Order().ToArray());
        Assert.Equal(2, page.Total);
        // A parent task with no work items is not a project and must never be listed.
        Assert.DoesNotContain(page.Items, x => x.Title == "Việc lẻ");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Projects}/{a.SoloTaskId}")).StatusCode);
        // Nor is a work item itself addressable as a project.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Projects}/{a.FirstWorkItemId}")).StatusCode);

        var alpha = Assert.Single(page.Items, x => x.Title == "Dự án Alpha");
        Assert.Equal(4, alpha.WorkItemCount);
        Assert.Equal(1, alpha.CompletedWorkItemCount);
        Assert.Equal(2, alpha.ParticipantCount);
        Assert.Equal(0, alpha.UnassignedWorkItemCount);
        // Weighted mean of 40, 100, 0 and 5 at equal Medium weight.
        Assert.Equal(36, alpha.ProgressPercent);
        Assert.Equal("SMALL", alpha.SizeCategory);
        Assert.Equal("Đồng nghiệp", alpha.OwnerName);
    }

    [Fact]
    public async Task The_responsibility_split_covers_the_whole_project_and_sums_to_one_hundred()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var detail = (await client.GetFromJsonAsync<ProjectDashboardDetailView>($"{Projects}/{a.AlphaId}"))!;

        Assert.True(detail.CanSeeAllParticipants);
        Assert.Equal(0, detail.HiddenWorkItemCount);
        Assert.Equal(0, detail.HiddenParticipantCount);
        Assert.Equal(4, detail.WorkItems.Count);
        Assert.Equal(100m, detail.Participants.Sum(x => x.ResponsibilityPercent));
        Assert.Equal(4, detail.Participants.Count);
        Assert.All(detail.Participants, x => Assert.Equal(25.0m, x.ResponsibilityPercent));

        // Work routed to a department but not yet claimed is reported as its own row, never dropped.
        var unclaimed = detail.Participants.Where(x => x.UserId is null).ToArray();
        Assert.Equal(2, unclaimed.Length);
        Assert.All(unclaimed, x => Assert.NotNull(x.DepartmentName));

        var inProgress = Assert.Single(detail.WorkItems, x => x.Status == "IN_PROGRESS");
        Assert.Equal(4, inProgress.ChecklistTotal);
        Assert.Equal(2, inProgress.ChecklistDone);
        Assert.Equal(40, inProgress.ProgressPercent);
        Assert.Equal(2, inProgress.Weight);
        var owner = Assert.Single(detail.Participants, x => x.UserId == a.User);
        Assert.Equal("Phần việc A1", owner.CurrentWorkItemTitle);
        Assert.Equal(27.6m, owner.ContributionPercent);
    }

    [Fact]
    public async Task An_employee_sees_the_shared_project_header_but_not_other_peoples_rows()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var page = (await client.GetFromJsonAsync<ProjectDashboardPage>(Projects))!;
        // Reached only through the one work item they hold; the colleague's project stays invisible.
        Assert.Equal("Dự án Alpha", Assert.Single(page.Items).Title);
        Assert.Equal(1, page.Total);

        var detail = (await client.GetFromJsonAsync<ProjectDashboardDetailView>($"{Projects}/{a.AlphaId}"))!;
        Assert.False(detail.CanSeeAllParticipants);
        Assert.Equal(new[] { "Phần việc A1", "Phần việc A4" }, detail.WorkItems.Select(x => x.Title).Order().ToArray());
        Assert.Equal(2, detail.HiddenWorkItemCount);
        var self = Assert.Single(detail.Participants);
        Assert.Equal(a.User, self.UserId);
        Assert.True(self.IsSelf);
        Assert.Equal(3, detail.HiddenParticipantCount);
        // Their own share is a share of the whole project, not of the slice they can see.
        Assert.Equal(25.0m, detail.MyResponsibilityPercent);
        Assert.Equal(27.6m, detail.MyContributionPercent);
        // The shared header still reports the true project totals.
        Assert.Equal(4, detail.Project.WorkItemCount);
        Assert.Equal(36, detail.Project.ProgressPercent);
        Assert.DoesNotContain(detail.Participants, x => x.UserId == a.Colleague);
    }

    [Fact]
    public async Task An_employee_cannot_read_another_persons_project_or_the_team_dashboard()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // A project they take no part in is answered as missing, not as forbidden.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Projects}/{a.BetaId}")).StatusCode);

        // Reading other people is a management capability they do not hold.
        using var team = await client.GetAsync(Team);
        Assert.Equal(HttpStatusCode.Forbidden, team.StatusCode);
        Assert.Contains("ACCESS.DENIED", await team.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Team}?departmentId={a.OtherDepartment}")).StatusCode);
    }

    [Fact]
    public async Task A_manager_reads_their_own_subordinates_and_is_refused_another_managers_department()
    {
        var a = await SeedAsync("MANAGER", manageOwnDepartment: true);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TeamDashboardView>(Team))!;
        Assert.False(view.TenantWide);
        Assert.Equal(new[] { "Đồng nghiệp", "Test Employee" }, view.Members.Select(x => x.FullName).Order().ToArray());
        Assert.DoesNotContain(view.Members, x => x.FullName == "Người phòng khác");
        Assert.False(view.Truncated);
        Assert.Equal(100m, view.Members.Sum(x => x.ResponsibilitySharePercent));
        var self = Assert.Single(view.Members, x => x.UserId == a.User);
        Assert.Equal(1, self.ActiveWorkItems);
        Assert.Equal(40, self.AvgProgressPercent);

        // The other department belongs to nobody this manager manages.
        using var refused = await client.GetAsync($"{Team}?departmentId={a.OtherDepartment}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("TASK.TARGET_OUT_OF_SCOPE", await refused.Content.ReadAsStringAsync());

        // Their own department is allowed.
        var narrowed = (await client.GetFromJsonAsync<TeamDashboardView>($"{Team}?departmentId={a.OwnDepartment}"))!;
        Assert.Equal(2, narrowed.Members.Count);
        Assert.Equal(a.OwnDepartment, Assert.Single(narrowed.ScopeDepartmentIds));
    }

    [Fact]
    public async Task A_manager_without_any_configured_scope_reads_an_empty_team_instead_of_failing()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TeamDashboardView>(Team))!;

        Assert.Empty(view.Members);
        Assert.Empty(view.Departments);
        Assert.Empty(view.ScopeDepartmentIds);
        Assert.False(view.TenantWide);
    }

    [Fact]
    public async Task A_manager_sees_only_the_project_rows_inside_their_scope()
    {
        var a = await SeedAsync("MANAGER", manageOwnDepartment: true);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var page = (await client.GetFromJsonAsync<ProjectDashboardPage>(Projects))!;
        Assert.Equal("Dự án Alpha", Assert.Single(page.Items).Title);

        var detail = (await client.GetFromJsonAsync<ProjectDashboardDetailView>($"{Projects}/{a.AlphaId}"))!;
        Assert.True(detail.CanSeeAllParticipants);
        Assert.Equal(3, detail.WorkItems.Count);
        Assert.DoesNotContain(detail.WorkItems, x => x.Title == "Phần việc A3");
        Assert.Equal(1, detail.HiddenWorkItemCount);
        // The person holding the out-of-scope row is not named to this manager.
        Assert.Equal(1, detail.HiddenParticipantCount);
        Assert.Equal(3, detail.Participants.Count);
        Assert.Equal(4, detail.Project.WorkItemCount);
        Assert.Equal(36, detail.Project.ProgressPercent);
    }

    [Fact]
    public async Task A_company_wide_reader_sees_every_department_in_the_team_dashboard()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TeamDashboardView>(Team))!;

        Assert.True(view.TenantWide);
        Assert.Equal(3, view.Members.Count);
        Assert.Equal(2, view.Departments.Count);
        Assert.Empty(view.ScopeDepartmentIds);
        Assert.Equal(TeamDashboardQuery.MemberLimit, view.MemberLimit);
    }

    [Fact]
    public async Task A_personal_dashboard_only_ever_reports_the_callers_own_work()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<MyWorkDashboardView>(MyWork))!;

        Assert.Equal(1, view.Total);
        Assert.Equal("Phần việc A1", Assert.Single(view.Items).WorkItem.Title);
        Assert.Equal("Dự án Alpha", view.Items[0].ProjectTitle);
        Assert.Equal(a.AlphaId, view.Items[0].ProjectId);
        Assert.Equal(40, view.MyProgressPercent);
        Assert.Equal(1, view.ActiveWorkItems);
        Assert.Equal(0, view.CompletedWorkItems);
        // Work routed to their department but unclaimed is counted apart from their own progress.
        Assert.Equal(1, view.PendingDepartmentClaims);
        Assert.Equal(1, view.ByStatus["IN_PROGRESS"]);
        Assert.Equal("Dự án Alpha", Assert.Single(view.Projects).Title);
        Assert.Equal(36, view.Projects[0].ProgressPercent);

        // There is no parameter that could aim this endpoint at another account.
        var spoofed = (await client.GetFromJsonAsync<MyWorkDashboardView>($"{MyWork}?userId={a.Colleague}"))!;
        Assert.Equal(view.Total, spoofed.Total);
        Assert.Equal("Phần việc A1", Assert.Single(spoofed.Items).WorkItem.Title);
    }

    [Fact]
    public async Task One_tenants_dashboard_never_reaches_another_tenants_projects()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        var page = (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?tenantId={foreign.Tenant}"))!;
        Assert.Equal(2, page.Total);
        Assert.DoesNotContain(page.Items, x => x.ProjectId == foreign.AlphaId);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Projects}/{foreign.AlphaId}")).StatusCode);
        // A department of another tenant does not exist as far as this caller is concerned.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Team}?departmentId={foreign.OwnDepartment}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Team}?departmentId={Guid.NewGuid()}")).StatusCode);

        var team = (await client.GetFromJsonAsync<TeamDashboardView>(Team))!;
        Assert.DoesNotContain(team.Members, x => x.UserId == foreign.User);
        Assert.Equal(3, team.Members.Count);
    }

    [Fact]
    public async Task Classification_search_and_department_filters_translate_and_page_correctly()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // Both seeded projects are small, so the classification filter must split them cleanly.
        Assert.Equal(2, (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?size=SMALL"))!.Total);
        Assert.Equal(0, (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?size=LARGE"))!.Total);
        Assert.Equal(2, (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?size=small"))!.Total);

        var searched = (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?search=%20alpha%20"))!;
        Assert.Equal("Dự án Alpha", Assert.Single(searched.Items).Title);
        Assert.Empty((await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?search=%25"))!.Items);

        var ours = (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?departmentId={a.OwnDepartment}"))!;
        Assert.Equal("Dự án Alpha", Assert.Single(ours.Items).Title);

        var first = (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<ProjectDashboardPage>($"{Projects}?pageSize=1&page=2"))!;
        Assert.Equal(2, first.Total);
        Assert.Equal(2, second.Total);
        Assert.Empty(first.Items.Select(x => x.ProjectId).Intersect(second.Items.Select(x => x.ProjectId)));

        foreach (var query in new[] { "?page=0", "?pageSize=0", "?pageSize=101", "?size=HUGE", "?search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Projects + query)).StatusCode);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?status=INPROGRESS" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(MyWork + query)).StatusCode);
    }

    [Fact]
    public async Task Legacy_rows_without_assignments_checklists_or_deadlines_report_zero_rather_than_failing()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(a.User, a.Tenant);
        var now = DateTimeOffset.UtcNow;

        // A project shaped like pre-feature data: a parent task with one untouched draft work item.
        var legacy = WorkTask.CreateDraft(a.Tenant, a.User, "Dự án cũ", now);
        db.Add(legacy);
        await db.SaveChangesAsync();
        var child = WorkTask.CreateDraft(a.Tenant, a.User, "Phần việc cũ", now, parentTaskId: legacy.Id);
        db.Add(child);
        await db.SaveChangesAsync();

        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var detail = (await client.GetFromJsonAsync<ProjectDashboardDetailView>($"{Projects}/{legacy.Id}"))!;

        Assert.Equal(0, detail.Project.ProgressPercent);
        Assert.Equal(1, detail.Project.WorkItemCount);
        Assert.Equal(0, detail.Project.ParticipantCount);
        Assert.Equal(1, detail.Project.UnassignedWorkItemCount);
        Assert.Equal(0, detail.Project.OverdueWorkItemCount);
        Assert.Equal("SMALL", detail.Project.SizeCategory);
        Assert.Empty(detail.Participants);
        Assert.Equal(0m, detail.MyResponsibilityPercent);
        var row = Assert.Single(detail.WorkItems);
        Assert.Equal(0, row.ProgressPercent);
        Assert.Equal(0, row.ChecklistTotal);
        Assert.Null(row.Deadline);
        Assert.Null(row.AssigneeUserId);
        Assert.False(row.IsOverdue);
        // The creator owns a project nobody has been assigned to yet.
        Assert.Equal(a.User, detail.Project.OwnerUserId);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task Every_dashboard_requires_authentication_and_is_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        foreach (var endpoint in new[] { Projects, MyWork, Team, $"{Projects}/{a.AlphaId}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint)).StatusCode);

        await LoginAsync(client, a);
        foreach (var endpoint in new[] { Projects, MyWork, Team, $"{Projects}/{a.AlphaId}" })
        {
            using var response = await client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
        }
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private async Task<Account> SeedAsync(string? roleName, bool manageOwnDepartment = false)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var ours = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, now);
        var other = Department.Create(tenant.Id, "OTHER", "Phòng khác", null, now);
        db.AddRange(ours, other);
        await db.SaveChangesAsync();

        var colleague = UserAccount.CreateTenantUser(tenant.Id, "COLLEAGUE", "colleague@example.test",
            "Đồng nghiệp", "fixture-only", now, ours.Id);
        var outsider = UserAccount.CreateTenantUser(tenant.Id, "OUTSIDER", "outsider@example.test",
            "Người phòng khác", "fixture-only", now, other.Id);
        db.AddRange(colleague, outsider);
        await db.SaveChangesAsync();

        // Project Alpha is owned by a colleague on purpose: the acting employee reaches it only
        // through the single work item they hold, which is exactly the scenario to verify.
        var alpha = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Dự án Alpha", now);
        var beta = WorkTask.CreateDraft(tenant.Id, outsider.Id, "Dự án Beta", now);
        var solo = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc lẻ", now);
        db.AddRange(alpha, beta, solo);
        await db.SaveChangesAsync();

        var a1 = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Phần việc A1", now, parentTaskId: alpha.Id);
        var a2 = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Phần việc A2", now, parentTaskId: alpha.Id);
        var a3 = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Phần việc A3", now, parentTaskId: alpha.Id);
        var a4 = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Phần việc A4", now, parentTaskId: alpha.Id);
        var b1 = WorkTask.CreateDraft(tenant.Id, outsider.Id, "Phần việc B1", now, parentTaskId: beta.Id);
        db.AddRange(a1, a2, a3, a4, b1);
        await db.SaveChangesAsync();

        // Checklist items must be added while the owning task is still a draft.
        var checklist = Enumerable.Range(1, 4)
            .Select(order => TaskChecklistItem.Create(a1.Id, $"Mục {order}", order)).ToArray();
        db.AddRange(checklist);
        await db.SaveChangesAsync();

        db.AddRange(
            TaskAssignment.Create(alpha.Id, colleague.Id, null, colleague.Id, now),
            TaskAssignment.Create(a1.Id, colleague.Id, null, user.Id, now),
            TaskAssignment.Create(a2.Id, colleague.Id, null, colleague.Id, now),
            TaskAssignment.Create(a3.Id, colleague.Id, other.Id, null, now),
            TaskAssignment.Create(a4.Id, colleague.Id, ours.Id, null, now),
            TaskAssignment.Create(beta.Id, outsider.Id, null, outsider.Id, now),
            TaskAssignment.Create(b1.Id, outsider.Id, null, outsider.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        var completedAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={ours.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            UPDATE "Task" SET "Status"='IN_PROGRESS' WHERE "TaskId" IN ({alpha.Id}, {a1.Id});
            UPDATE "Task" SET "Status"='COMPLETED', "CompletedAt"={completedAt} WHERE "TaskId"={a2.Id};
            UPDATE "Task" SET "Status"='ASSIGNED' WHERE "TaskId"={a4.Id};
            UPDATE "TaskChecklistItem" SET "IsCompleted"=TRUE, "CompletedBy"={user.Id}, "CompletedAt"={completedAt}
                WHERE "ChecklistItemId" IN ({checklist[0].Id}, {checklist[1].Id});
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        if (manageOwnDepartment)
        {
            db.Add(ManagementScope.Create(tenant.Id, user.Id, ours.Id, true, user.Id, now));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id, outsider.Id,
            ours.Id, other.Id, alpha.Id, beta.Id, solo.Id, a1.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(
        Guid User, Guid Tenant, string Key, Guid Colleague, Guid Outsider,
        Guid OwnDepartment, Guid OtherDepartment, Guid AlphaId, Guid BetaId, Guid SoloTaskId, Guid FirstWorkItemId);
}
