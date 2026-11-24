using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for bulk actions (F10) and the in-item activity feed (F11).
///
/// The point of F10 is that a batch is not a shortcut: every task goes through the same
/// single-task command, so one task the caller may not touch fails on its own while the others
/// still succeed. The point of F11 is that the feed is gated by the item's own read policy, not
/// by the auditor's permission.
/// </summary>
[Collection("Postgres")]
public sealed class BulkAndActivityApiTests(PostgresFixture database)
{
    private const string Password = "Bulk-activity-test!8812";

    private static string Activity(Guid taskId) => $"/api/v1/work-items/TASK/{taskId}/activity";

    [Fact]
    public async Task A_batch_reports_each_item_and_one_failure_does_not_undo_the_rest()
    {
        var a = await SeedAsync("MANAGER", scope: true);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // One real task plus an id that does not exist: the batch must do both and report both.
        var result = (await (await client.PostAsJsonAsync("/api/v1/tasks/bulk/assign", new
        {
            taskIds = new[] { a.DraftTaskId, Guid.NewGuid() },
            targetType = "USER",
            targetId = a.Colleague,
            note = "Giao lại hàng loạt"
        })).Content.ReadFromJsonAsync<BulkResult>())!;

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);
        var succeeded = Assert.Single(result.Items, item => item.Succeeded);
        Assert.Equal(a.DraftTaskId, succeeded.TaskId);
        var failed = Assert.Single(result.Items, item => !item.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(failed.Code));

        // The one that worked really moved, through the ordinary command.
        await using var db = database.Create(a.User, a.Tenant);
        var assigned = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == a.DraftTaskId);
        Assert.Equal(TaskState.Assigned, assigned.Status);
    }

    [Fact]
    public async Task A_batch_is_bounded_and_refuses_an_empty_selection()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/assign",
            new { taskIds = Array.Empty<Guid>(), targetType = "USER", targetId = a.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/assign",
            new { taskIds = new[] { a.DraftTaskId }, targetType = "USER", targetId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/archive",
            new
            {
                taskIds = Enumerable.Range(0, TaskBulkOperations.MaxItems + 1)
                    .Select(_ => Guid.NewGuid()).ToArray()
            })).StatusCode);
    }

    [Fact]
    public async Task A_batch_cannot_reach_a_task_the_caller_has_no_authority_over()
    {
        var a = await SeedAsync("EMPLOYEE");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // An employee holds no assignment authority, and the foreign task is in another tenant.
        var result = (await (await client.PostAsJsonAsync("/api/v1/tasks/bulk/assign", new
        {
            taskIds = new[] { a.DraftTaskId, foreign.DraftTaskId },
            targetType = "USER",
            targetId = a.Colleague
        })).Content.ReadFromJsonAsync<BulkResult>())!;

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(2, result.Failed);

        // Nothing moved in either workspace.
        await using var theirs = database.Create(foreign.User, foreign.Tenant);
        Assert.Equal(TaskState.Draft,
            (await theirs.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == foreign.DraftTaskId)).Status);
    }

    [Fact]
    public async Task The_activity_feed_shows_what_happened_to_an_item_the_caller_can_open()
    {
        var a = await SeedAsync("MANAGER", scope: true);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // Do something real so there is an audit entry to read back.
        using var assigned = await client.PostAsJsonAsync($"/api/v1/tasks/{a.DraftTaskId}/assign",
            new { targetType = "USER", targetId = a.Colleague, note = "Giao việc" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var feed = (await client.GetFromJsonAsync<ActivityPage>(Activity(a.DraftTaskId)))!;

        Assert.Equal("TASK", feed.ObjectType);
        Assert.Equal(a.DraftTaskId, feed.ObjectId);
        Assert.NotEmpty(feed.Items);
        Assert.All(feed.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Action)));
        // The actor is named, and it is the person who acted.
        Assert.Contains(feed.Items, item => item.ActorId == a.User && item.ActorName == "Test Employee");
        // The feed never carries the before/after payloads the audit viewer shows.
        var raw = await (await client.GetAsync(Activity(a.DraftTaskId))).Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"before\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"after\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task The_feed_needs_no_auditor_permission_but_does_need_to_see_the_item()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // An ordinary employee has no audit.read permission, yet can read the feed of work they
        // can open — that is the whole difference from the audit viewer.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Activity(a.OwnTaskId))).StatusCode);

        // A task outside their scope is reported as missing.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Activity(a.ForeignScopeTaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Activity(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"/api/v1/work-items/PROJECT/{a.OwnTaskId}/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"{Activity(a.OwnTaskId)}?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task One_tenants_feed_never_reaches_another_tenants_item()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Activity(foreign.DraftTaskId))).StatusCode);
    }

    [Fact]
    public async Task Turning_the_flags_off_hides_both_and_leaves_tasks_and_audit_alone()
    {
        // Two callers, because the two capabilities live in different roles: assignment authority
        // is a manager's, and the audit viewer is an administrator's. A company administrator also
        // may not assign a company-level task to an individual, so one account cannot do both.
        var a = await SeedAsync("MANAGER", scope: true);
        var auditor = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?>
        {
            ["Features:BulkActions"] = "false",
            ["Features:ActivityFeed"] = "false"
        });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/assign",
            new { taskIds = new[] { a.DraftTaskId }, targetType = "USER", targetId = a.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/archive",
            new { taskIds = new[] { a.DraftTaskId } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Activity(a.DraftTaskId))).StatusCode);

        // The endpoints that existed before keep working exactly as they did.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/tasks/{a.DraftTaskId}/assign",
            new { targetType = "USER", targetId = a.Colleague, note = "Vẫn giao được" })).StatusCode);

        using var adminClient = factory.CreateClient();
        await LoginAsync(adminClient, auditor);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/v1/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Both_require_authentication_and_are_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Activity(a.DraftTaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/tasks/bulk/archive",
            new { taskIds = new[] { a.DraftTaskId } })).StatusCode);

        await LoginAsync(client, a);
        using var response = await client.GetAsync(Activity(a.DraftTaskId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    /// <param name="roles">
    /// Comma-separated system roles to grant. The seeded grants are narrow: <c>tasks.assign</c>
    /// belongs to MANAGER and <c>audit.read</c> to COMPANY_ADMIN, so a test that needs both asks
    /// for both rather than assuming one role implies the other.
    /// </param>
    /// <param name="scope">
    /// Assignment needs both the permission and a management scope over the target's department,
    /// exactly as <c>TaskAssignmentApiTests</c> sets it up. Only the tests that really assign ask
    /// for one, so the tests about a caller without authority keep seeing nothing.
    /// </param>
    private async Task<Account> SeedAsync(string? roles, bool scope = false)
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

        var draft = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc nháp", now);
        var own = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc của tôi", now);
        var foreignScope = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Việc của người khác", now);
        db.AddRange(draft, own, foreignScope);
        await db.SaveChangesAsync();
        db.AddRange(
            TaskAssignment.Create(own.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(foreignScope.Id, colleague.Id, null, colleague.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        foreach (var roleName in (roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        if (scope)
        {
            db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, user.Id, department.Id, false, user.Id, now));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id, draft.Id, own.Id, foreignScope.Id);
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
        Guid DraftTaskId, Guid OwnTaskId, Guid ForeignScopeTaskId);
}
