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
/// End-to-end checks for task dependencies (F7).
///
/// Two things matter. First, the links are advisory: a task whose predecessor is still open can
/// still be worked on, because the lifecycle policy is untouched — the feed only reports the fact.
/// Second, a link may only be drawn between two tasks the caller can already open, so the feature
/// cannot be used to discover work outside the caller's scope or in another tenant.
/// </summary>
[Collection("Postgres")]
public sealed class TaskDependencyApiTests(PostgresFixture database)
{
    private const string Password = "Dependency-test!7741";

    private static string Endpoint(Guid taskId) => $"/api/v1/tasks/{taskId}/dependencies";

    [Fact]
    public async Task A_link_is_recorded_once_reported_both_ways_and_removed_softly()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await (await client.PostAsJsonAsync(Endpoint(a.Second),
            new { dependsOnTaskId = a.First })).Content.ReadFromJsonAsync<TaskDependencyView>())!;

        Assert.Equal(a.Second, view.TaskId);
        var predecessor = Assert.Single(view.DependsOn);
        Assert.Equal(a.First, predecessor.TaskId);
        Assert.Equal("Việc thứ nhất", predecessor.Title);
        Assert.Equal("DRAFT", predecessor.Status);
        Assert.False(predecessor.Settled);
        Assert.Empty(view.Blocks);
        // A draft predecessor is open, so the waiting task is reported as held up.
        Assert.Equal(1, view.BlockedBy);

        // Posting the same link again is not an error and does not duplicate the row.
        var again = (await (await client.PostAsJsonAsync(Endpoint(a.Second),
            new { dependsOnTaskId = a.First })).Content.ReadFromJsonAsync<TaskDependencyView>())!;
        Assert.Single(again.DependsOn);

        // The other end sees the mirror image.
        var other = (await client.GetFromJsonAsync<TaskDependencyView>(Endpoint(a.First)))!;
        Assert.Empty(other.DependsOn);
        Assert.Equal(a.Second, Assert.Single(other.Blocks).TaskId);
        Assert.Equal(0, other.BlockedBy);

        var removed = (await (await client.DeleteAsync($"{Endpoint(a.Second)}/{a.First}"))
            .Content.ReadFromJsonAsync<TaskDependencyView>())!;
        Assert.Empty(removed.DependsOn);
        Assert.Equal(0, removed.BlockedBy);

        // Removal is soft: the row survives with RemovedAt set, so history is not rewritten.
        await using var db = database.Create(a.User, a.Tenant);
        var row = await db.TaskDependencies.AsNoTracking()
            .SingleAsync(d => d.TaskId == a.Second && d.DependsOnTaskId == a.First);
        Assert.NotNull(row.RemovedAt);

        // Re-linking reuses that row rather than inserting a second one.
        Assert.Single((await (await client.PostAsJsonAsync(Endpoint(a.Second),
            new { dependsOnTaskId = a.First })).Content.ReadFromJsonAsync<TaskDependencyView>())!.DependsOn);
        Assert.Equal(1, await db.TaskDependencies.AsNoTracking()
            .CountAsync(d => d.TaskId == a.Second && d.DependsOnTaskId == a.First));
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task A_settled_predecessor_stops_counting_as_a_block()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        await client.PostAsJsonAsync(Endpoint(a.Second), new { dependsOnTaskId = a.First });

        // Move the predecessor to a finished state the way the platform's own data would look.
        await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Task\" SET \"Status\"='COMPLETED' WHERE \"TaskId\"={a.First}");

        var view = (await client.GetFromJsonAsync<TaskDependencyView>(Endpoint(a.Second)))!;

        Assert.True(Assert.Single(view.DependsOn).Settled);
        Assert.Equal(0, view.BlockedBy);
    }

    [Fact]
    public async Task A_loop_a_self_link_and_an_oversized_fan_in_are_all_refused()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Endpoint(a.Second),
            new { dependsOnTaskId = a.First })).StatusCode);

        // The reverse link would make the two wait for each other.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = a.Second })).StatusCode);
        // So would a longer loop: third waits for second, then first waits for third.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Endpoint(a.Third),
            new { dependsOnTaskId = a.Second })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = a.Third })).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = a.First })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = Guid.Empty })).StatusCode);
        // An unmapped member is refused, like everywhere else on the platform.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = a.Second, note = "không có trường này" })).StatusCode);

        // Nothing beyond the two accepted links was written.
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(2, await db.TaskDependencies.AsNoTracking().CountAsync(d => d.RemovedAt == null));
    }

    [Fact]
    public async Task Neither_end_may_be_a_task_the_caller_cannot_open()
    {
        var a = await SeedAsync("MANAGER");
        var foreign = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // A task in another tenant is reported as missing from both ends of the link.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Endpoint(a.First),
            new { dependsOnTaskId = foreign.First })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Endpoint(foreign.First),
            new { dependsOnTaskId = a.First })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Endpoint(foreign.First))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Endpoint(Guid.NewGuid()))).StatusCode);

        // An employee sees only their own work, so a colleague's task is out of reach.
        var employee = await SeedAsync("EMPLOYEE");
        using var employeeClient = factory.CreateClient();
        await LoginAsync(employeeClient, employee);
        Assert.Equal(HttpStatusCode.NotFound,
            (await employeeClient.GetAsync(Endpoint(employee.ColleagueTask))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employeeClient.PostAsJsonAsync(Endpoint(employee.Own),
            new { dependsOnTaskId = employee.ColleagueTask })).StatusCode);

        await using var db = database.Create(a.User, a.Tenant);
        Assert.False(await db.TaskDependencies.AsNoTracking().AnyAsync());
    }

    [Fact]
    public async Task An_open_predecessor_warns_but_never_blocks_the_lifecycle()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        await client.PostAsJsonAsync(Endpoint(a.Second), new { dependsOnTaskId = a.First });
        Assert.Equal(1, (await client.GetFromJsonAsync<TaskDependencyView>(Endpoint(a.Second)))!.BlockedBy);

        // The waiting task can still be assigned, exactly as it could before the feature existed.
        using var assigned = await client.PostAsJsonAsync($"/api/v1/tasks/{a.Second}/assign",
            new { targetType = "USER", targetId = a.Colleague });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(TaskState.Assigned, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == a.Second)).Status);
        // The warning is still there afterwards; it is information, not a gate.
        Assert.Equal(1, (await client.GetFromJsonAsync<TaskDependencyView>(Endpoint(a.Second)))!.BlockedBy);
    }

    [Fact]
    public async Task The_flag_hides_the_endpoints_and_authentication_is_required()
    {
        var a = await SeedAsync("MANAGER");
        await using var disabled = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:TaskDependencies"] = "false" });
        using var off = disabled.CreateClient();
        await LoginAsync(off, a);

        Assert.Equal(HttpStatusCode.NotFound, (await off.GetAsync(Endpoint(a.First))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await off.PostAsJsonAsync(Endpoint(a.Second),
            new { dependsOnTaskId = a.First })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await off.DeleteAsync($"{Endpoint(a.Second)}/{a.First}")).StatusCode);
        // With the flag off the task endpoints behave exactly as they did before.
        Assert.Equal(HttpStatusCode.OK, (await off.GetAsync("/api/v1/tasks")).StatusCode);

        await using var enabled = new AuthenticationFactory(database);
        using var anonymous = enabled.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Endpoint(a.First))).StatusCode);

        using var client = enabled.CreateClient();
        await LoginAsync(client, a);
        using var response = await client.GetAsync(Endpoint(a.First));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private async Task<Account> SeedAsync(string roleName)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var department = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, now);
        db.Add(department);
        await db.SaveChangesAsync();
        var colleague = UserAccount.CreateTenantUser(tenant.Id, "COLLEAGUE", "colleague@example.test",
            "Đồng nghiệp", "fixture-only", now, department.Id);
        db.Add(colleague);
        await db.SaveChangesAsync();

        var first = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc thứ nhất", now);
        var second = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc thứ hai", now);
        var third = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc thứ ba", now);
        var colleagueTask = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Việc của đồng nghiệp", now);
        db.AddRange(first, second, third, colleagueTask);
        await db.SaveChangesAsync();
        // The employee tests need one task of their own that the read policy lets them open.
        db.Add(TaskAssignment.Create(first.Id, user.Id, null, user.Id, now));
        db.Add(TaskAssignment.Create(colleagueTask.Id, colleague.Id, null, colleague.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
        db.UserRoles.Add(new(user.Id, role.Id));
        await db.SaveChangesAsync();

        // Assignment authority, for the test that proves a warning does not block the lifecycle.
        db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, user.Id, department.Id, false, user.Id, now));
        await db.SaveChangesAsync();

        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id,
            first.Id, second.Id, third.Id, first.Id, colleagueTask.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Colleague,
        Guid First, Guid Second, Guid Third, Guid Own, Guid ColleagueTask);
}
