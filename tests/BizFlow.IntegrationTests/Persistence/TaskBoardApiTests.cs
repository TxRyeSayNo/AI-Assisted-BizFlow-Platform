using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for the Kanban board projection (F1): the real SQL translation against
/// PostgreSQL, the authorization and tenant boundaries it inherits from TaskReadPolicy, and the
/// feature flag that must make the endpoint indistinguishable from one that was never deployed.
/// </summary>
[Collection("Postgres")]
public sealed class TaskBoardApiTests(PostgresFixture database)
{
    private const string Password = "Task-board-test!7741";
    private const string Board = "/api/v1/tasks/board";
    private const string Features = "/api/v1/features";

    [Fact]
    public async Task Every_lifecycle_state_becomes_a_lane_and_each_lane_reports_its_real_size()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TaskBoardView>(Board))!;

        // A lane exists for every state, empty or not, so the client never invents columns.
        Assert.Equal(Enum.GetValues<TaskState>().Length, view.Columns.Count);
        Assert.Equal(Enum.GetValues<TaskState>().Select(TaskListCodes.State).ToArray(),
            view.Columns.Select(column => column.Status).ToArray());
        Assert.Equal(50, view.ColumnSize);

        var draft = Lane(view, TaskState.Draft);
        Assert.Equal(2, draft.Total);
        Assert.Equal(2, draft.Items.Count);
        // Soonest deadline first, undated work last.
        Assert.Equal(["Việc gấp", "Việc không hạn"], draft.Items.Select(item => item.Title).ToArray());

        var inProgress = Lane(view, TaskState.InProgress);
        Assert.Equal("Việc đang làm", Assert.Single(inProgress.Items).Title);
        Assert.Equal(a.User, inProgress.Items[0].AssignedUserId);
        Assert.Equal("Test Employee", inProgress.Items[0].AssignedUserName);
        Assert.Equal("HIGH", inProgress.Items[0].Priority);

        var assigned = Lane(view, TaskState.Assigned);
        Assert.Equal(a.OtherDepartment, Assert.Single(assigned.Items).AssignedDepartmentId);
        Assert.Equal("Phòng khác", assigned.Items[0].AssignedDepartmentName);
        Assert.Null(assigned.Items[0].AssignedUserId);

        // The parent lane carries its own subtask roll-up, exactly as the list rows do.
        var parent = Assert.Single(Lane(view, TaskState.Accepted).Items);
        Assert.Equal("Dự án mẹ", parent.Title);
        Assert.Equal(1, parent.SubtaskCount);
        Assert.Equal(0, parent.CompletedSubtaskCount);

        Assert.Empty(Lane(view, TaskState.Cancelled).Items);
        Assert.Equal(0, Lane(view, TaskState.Cancelled).Total);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task A_capped_lane_still_reports_the_untruncated_total()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TaskBoardView>($"{Board}?columnSize=1"))!;

        var draft = Lane(view, TaskState.Draft);
        Assert.Single(draft.Items);
        Assert.Equal(2, draft.Total);
        Assert.Equal(1, view.ColumnSize);
    }

    [Fact]
    public async Task Assignee_department_priority_deadline_search_and_parent_filters_translate_correctly()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // Ordinal ordering keeps the expectations independent of the host's Vietnamese collation.
        async Task<string[]> TitlesAsync(string query)
        {
            var view = (await client.GetFromJsonAsync<TaskBoardView>(Board + query))!;
            return [.. view.Columns.SelectMany(column => column.Items).Select(item => item.Title)
                .OrderBy(title => title, StringComparer.Ordinal)];
        }

        // Every task whose live assignment names this user, across all four lanes they occupy.
        Assert.Equal(["Dự án mẹ", "Phần việc con", "Việc gấp", "Việc đang làm"],
            await TitlesAsync($"?assignedUserId={a.User}"));
        Assert.Equal("Việc phòng khác", Assert.Single(await TitlesAsync($"?assignedDepartmentId={a.OtherDepartment}")));
        Assert.Equal("Việc đang làm", Assert.Single(await TitlesAsync("?priority=HIGH")));
        Assert.Equal("Việc gấp", Assert.Single(await TitlesAsync("?search=%20g%E1%BA%A5p%20")));
        Assert.Empty(await TitlesAsync("?search=%25"));

        // A deadline window never matches undated work.
        var window = $"?deadlineFrom={Uri.EscapeDataString(a.Now.AddHours(-1).ToString("O"))}" +
            $"&deadlineTo={Uri.EscapeDataString(a.Now.AddDays(2).ToString("O"))}";
        Assert.Equal("Việc gấp", Assert.Single(await TitlesAsync(window)));

        var parentsOnly = await TitlesAsync("?isParentOnly=true");
        Assert.Contains("Dự án mẹ", parentsOnly);
        Assert.DoesNotContain("Phần việc con", parentsOnly);
    }

    [Fact]
    public async Task One_tenants_board_never_reaches_another_tenants_tasks()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        // Neither a spoofed header nor a spoofed query parameter can retarget the tenant.
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        var view = (await client.GetFromJsonAsync<TaskBoardView>($"{Board}?tenantId={foreign.Tenant}"))!;
        var rows = view.Columns.SelectMany(column => column.Items).ToArray();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.NotEqual(foreign.User, row.CreatorId));
        Assert.All(rows, row => Assert.NotEqual(foreign.User, row.AssignedUserId));
        Assert.All(rows, row => Assert.NotEqual(foreign.OtherDepartment, row.AssignedDepartmentId));
        Assert.DoesNotContain(rows, row => row.TaskId == foreign.UrgentTaskId);
        // Filtering by a foreign assignee or department yields nothing rather than foreign rows.
        Assert.Empty((await client.GetFromJsonAsync<TaskBoardView>($"{Board}?assignedUserId={foreign.User}"))!
            .Columns.SelectMany(column => column.Items));
        Assert.Empty((await client.GetFromJsonAsync<TaskBoardView>($"{Board}?assignedDepartmentId={foreign.OtherDepartment}"))!
            .Columns.SelectMany(column => column.Items));
    }

    [Fact]
    public async Task A_caller_without_any_task_read_grant_is_refused_and_an_anonymous_one_is_challenged()
    {
        var a = await SeedAsync(roleName: null);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Board)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Features)).StatusCode);

        await LoginAsync(client, a);
        using var refused = await client.GetAsync(Board);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("ACCESS.DENIED", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_employee_sees_only_the_work_their_scope_covers()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TaskBoardView>(Board))!;
        var titles = view.Columns.SelectMany(column => column.Items).Select(item => item.Title).ToArray();

        // Work routed to a department they do not belong to stays invisible.
        Assert.DoesNotContain("Việc phòng khác", titles);
        Assert.Contains("Việc đang làm", titles);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task Invalid_parameters_are_refused_before_storage_is_touched()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var inverted = $"?deadlineFrom={Uri.EscapeDataString(a.Now.AddDays(2).ToString("O"))}" +
            $"&deadlineTo={Uri.EscapeDataString(a.Now.ToString("O"))}";
        foreach (var query in new[]
        {
            "?columnSize=0", "?columnSize=101", "?priority=Highest",
            "?search=" + new string('a', TaskBoardQuery.MaxSearchLength + 1),
            $"?assignedUserId={Guid.Empty}", $"?assignedDepartmentId={Guid.Empty}", inverted
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Board + query)).StatusCode);
    }

    [Fact]
    public async Task The_board_is_never_cached_and_never_leaks_credentials()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var endpoint in new[] { Board, Features })
        {
            using var response = await client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_the_board_without_disturbing_the_task_list()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:Kanban"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Board)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<FeatureFlagsProbe>(Features))!.Kanban);

        // The endpoints that existed before the board keep working exactly as they did.
        var list = (await client.GetFromJsonAsync<TaskListPage>("/api/v1/tasks"))!;
        Assert.Equal(6, list.Total);
    }

    private static TaskBoardColumn Lane(TaskBoardView view, TaskState state) =>
        Assert.Single(view.Columns, column => column.Status == TaskListCodes.State(state));

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private async Task<Account> SeedAsync(string? roleName)
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
        db.Add(colleague);
        await db.SaveChangesAsync();

        var urgent = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc gấp", now, deadline: now.AddDays(1));
        var undated = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc không hạn", now);
        var running = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc đang làm", now,
            priority: TaskPriority.High, deadline: now.AddDays(7));
        var elsewhere = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Việc phòng khác", now);
        var parent = WorkTask.CreateDraft(tenant.Id, user.Id, "Dự án mẹ", now);
        db.AddRange(urgent, undated, running, elsewhere, parent);
        await db.SaveChangesAsync();

        var child = WorkTask.CreateDraft(tenant.Id, user.Id, "Phần việc con", now, parentTaskId: parent.Id);
        db.Add(child);
        await db.SaveChangesAsync();

        db.AddRange(
            TaskAssignment.Create(urgent.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(running.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(elsewhere.Id, colleague.Id, other.Id, null, now),
            TaskAssignment.Create(parent.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(child.Id, user.Id, null, user.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={ours.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            UPDATE "Task" SET "Status"='IN_PROGRESS' WHERE "TaskId"={running.Id};
            UPDATE "Task" SET "Status"='ASSIGNED' WHERE "TaskId"={elsewhere.Id};
            UPDATE "Task" SET "Status"='ACCEPTED' WHERE "TaskId"={parent.Id};
            UPDATE "Task" SET "Status"='SUBMITTED' WHERE "TaskId"={child.Id};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, ours.Id, other.Id, urgent.Id, now);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record FeatureFlagsProbe(bool Kanban);

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid OwnDepartment,
        Guid OtherDepartment, Guid UrgentTaskId, DateTimeOffset Now);
}
