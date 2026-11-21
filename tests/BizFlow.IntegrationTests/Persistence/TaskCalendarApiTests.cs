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
/// End-to-end checks for the read-only deadline calendar (F2): window filtering against
/// PostgreSQL, truncation reporting, the scope and tenant boundaries inherited from
/// TaskReadPolicy, and the Calendar feature flag.
/// </summary>
[Collection("Postgres")]
public sealed class TaskCalendarApiTests(PostgresFixture database)
{
    private const string Password = "Task-calendar-test!4480";
    private const string Calendar = "/api/v1/tasks/calendar";

    [Fact]
    public async Task Only_dated_work_inside_the_window_is_returned_and_it_comes_back_by_deadline()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TaskCalendarView>(Window(a.Now.AddDays(-1), a.Now.AddDays(10))))!;

        // Undated work never appears on a calendar, and neither does work outside the window.
        Assert.Equal(["Hạn hôm nay", "Hạn 3 ngày", "Hạn 9 ngày"], view.Items.Select(item => item.Title).ToArray());
        Assert.All(view.Items, item => Assert.NotNull(item.Deadline));
        Assert.False(view.Truncated);
        Assert.Equal(500, view.Limit);
        Assert.Null(factory.LastServerError);

        var narrow = (await client.GetFromJsonAsync<TaskCalendarView>(Window(a.Now.AddDays(-1), a.Now.AddDays(4))))!;
        Assert.Equal(["Hạn hôm nay", "Hạn 3 ngày"], narrow.Items.Select(item => item.Title).ToArray());
    }

    [Fact]
    public async Task A_window_holding_more_than_the_limit_reports_truncation_instead_of_lying()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<TaskCalendarView>(
            Window(a.Now.AddDays(-1), a.Now.AddDays(10)) + "&limit=2"))!;

        Assert.Equal(2, view.Items.Count);
        Assert.True(view.Truncated);

        var exact = (await client.GetFromJsonAsync<TaskCalendarView>(
            Window(a.Now.AddDays(-1), a.Now.AddDays(10)) + "&limit=3"))!;
        Assert.Equal(3, exact.Items.Count);
        Assert.False(exact.Truncated);
    }

    [Fact]
    public async Task Status_priority_assignee_and_department_filters_translate_correctly()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var window = Window(a.Now.AddDays(-1), a.Now.AddDays(10));

        async Task<string[]> TitlesAsync(string extra)
        {
            var view = (await client.GetFromJsonAsync<TaskCalendarView>(window + extra))!;
            return [.. view.Items.Select(item => item.Title)];
        }

        Assert.Equal("Hạn 3 ngày", Assert.Single(await TitlesAsync("&status=IN_PROGRESS")));
        Assert.Equal("Hạn 9 ngày", Assert.Single(await TitlesAsync("&priority=CRITICAL")));
        Assert.Equal(["Hạn hôm nay", "Hạn 3 ngày"], await TitlesAsync($"&assignedUserId={a.User}"));
        Assert.Equal("Hạn 9 ngày", Assert.Single(await TitlesAsync($"&assignedDepartmentId={a.OtherDepartment}")));
        Assert.Empty(await TitlesAsync($"&assignedUserId={Guid.NewGuid()}"));
    }

    [Fact]
    public async Task One_tenants_calendar_never_reaches_another_tenants_work()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());
        var window = Window(a.Now.AddDays(-1), a.Now.AddDays(10));

        var view = (await client.GetFromJsonAsync<TaskCalendarView>($"{window}&tenantId={foreign.Tenant}"))!;

        Assert.Equal(3, view.Items.Count);
        Assert.All(view.Items, item => Assert.NotEqual(foreign.User, item.CreatorId));
        Assert.DoesNotContain(view.Items, item => item.TaskId == foreign.TodayTaskId);
        Assert.Empty((await client.GetFromJsonAsync<TaskCalendarView>($"{window}&assignedUserId={foreign.User}"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<TaskCalendarView>($"{window}&assignedDepartmentId={foreign.OtherDepartment}"))!.Items);
    }

    [Fact]
    public async Task A_caller_without_any_task_read_grant_is_refused_and_an_anonymous_one_is_challenged()
    {
        var a = await SeedAsync(roleName: null);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var window = Window(a.Now.AddDays(-1), a.Now.AddDays(10));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(window)).StatusCode);

        await LoginAsync(client, a);
        using var refused = await client.GetAsync(window);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("ACCESS.DENIED", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_windows_are_refused_and_the_calendar_is_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var query in new[]
        {
            Window(a.Now.AddDays(1), a.Now),
            Window(a.Now, a.Now.AddDays(TaskCalendarQuery.MaxWindowDays + 1)),
            Window(a.Now, a.Now.AddDays(10)) + "&limit=0",
            Window(a.Now, a.Now.AddDays(10)) + $"&limit={TaskCalendarQuery.MaxLimit + 1}",
            Window(a.Now, a.Now.AddDays(10)) + "&status=InProgress",
            Window(a.Now, a.Now.AddDays(10)) + $"&assignedUserId={Guid.Empty}"
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(query)).StatusCode);

        using var response = await client.GetAsync(Window(a.Now.AddDays(-1), a.Now.AddDays(10)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_the_calendar_without_disturbing_the_task_list()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:Calendar"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(Window(a.Now.AddDays(-1), a.Now.AddDays(10)))).StatusCode);
        Assert.Equal(4, (await client.GetFromJsonAsync<TaskListPage>("/api/v1/tasks"))!.Total);
        // The Kanban flag is independent of the calendar flag.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tasks/board")).StatusCode);
    }

    private static string Window(DateTimeOffset from, DateTimeOffset to) =>
        $"{Calendar}?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

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

        var today = WorkTask.CreateDraft(tenant.Id, user.Id, "Hạn hôm nay", now, deadline: now.AddHours(2));
        var soon = WorkTask.CreateDraft(tenant.Id, user.Id, "Hạn 3 ngày", now, deadline: now.AddDays(3));
        var later = WorkTask.CreateDraft(tenant.Id, user.Id, "Hạn 9 ngày", now,
            priority: TaskPriority.Critical, deadline: now.AddDays(9));
        var undated = WorkTask.CreateDraft(tenant.Id, user.Id, "Không hạn", now);
        db.AddRange(today, soon, later, undated);
        await db.SaveChangesAsync();

        db.AddRange(
            TaskAssignment.Create(today.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(soon.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(later.Id, user.Id, other.Id, null, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={ours.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            UPDATE "Task" SET "Status"='IN_PROGRESS' WHERE "TaskId"={soon.Id};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, ours.Id, other.Id, today.Id, now);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid OwnDepartment,
        Guid OtherDepartment, Guid TodayTaskId, DateTimeOffset Now);
}
