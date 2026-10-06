using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

public sealed partial class TaskListApiTests
{
    [Fact]
    public async Task Different_keys_cannot_assign_the_same_draft_twice_and_foreign_task_is_not_found()
    {
        var a = await SeedAsync("MANAGER"); var other = await SeedAsync("MANAGER"); var department = await ScopeAsync(a);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var draft = (await (await CreateAsync(client, new { title = "Concurrent assignment" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var responses = await Task.WhenAll(AssignAsync(client, draft.TaskId, new { targetType = "USER", targetId = a.Colleague }, Guid.NewGuid().ToString()),
            AssignAsync(client, draft.TaskId, new { targetType = "DEPARTMENT", targetId = department }, Guid.NewGuid().ToString()));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(1, await db.TaskAssignments.CountAsync(t => t.TaskId == draft.TaskId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(t => t.ObjectId == draft.TaskId && t.Action == "TASK.ASSIGNED"));
        await using var foreign = database.Create(other.User, other.Tenant); var foreignId = await foreign.WorkTasks.Select(t => t.Id).FirstAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await AssignAsync(client, foreignId, new { targetType = "USER", targetId = a.Colleague })).StatusCode);
    }
    [Fact]
    public async Task Assignment_requires_action_permission_and_active_target_even_when_directory_is_readable()
    {
        var employee = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, employee);
        await using var employeeDb = database.Create(employee.User, employee.Tenant);
        var employeeTask = await employeeDb.WorkTasks.FirstAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await AssignAsync(client, employeeTask.Id, new { targetType = "USER", targetId = employee.Colleague })).StatusCode);
        var manager = await SeedAsync("MANAGER"); await ScopeAsync(manager); await LoginAsync(client, manager);
        var draft = (await (await CreateAsync(client, new { title = "Inactive target" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        await using var db = database.Create(manager.User, manager.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={manager.Colleague}");
        Assert.Equal(HttpStatusCode.NotFound, (await AssignAsync(client, draft.TaskId, new { targetType = "USER", targetId = manager.Colleague })).StatusCode);
        Assert.Equal(TaskState.Draft, (await db.WorkTasks.SingleAsync(t => t.Id == draft.TaskId)).Status);
        Assert.False(await db.TaskAssignments.AnyAsync(t => t.TaskId == draft.TaskId));
    }

    private static async Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid task, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{task}/assign") { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private async Task<Guid> ScopeAsync(Account account)
    {
        await using var db = database.Create(account.User, account.Tenant);
        var department = (await db.Users.SingleAsync(u => u.Id == account.Colleague)).DepartmentId!.Value;
        db.ManagementScopes.Add(ManagementScope.Create(account.Tenant, account.User, department, false, account.User, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); return department;
    }
    [Fact]
    public async Task Assignment_replays_once_with_atomic_state_audit_notifications_and_assigned_visibility()
    {
        var a = await SeedAsync("MANAGER"); await ScopeAsync(a);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var draft = (await (await CreateAsync(client, new { title = "Assign me" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var key = Guid.NewGuid().ToString(); var body = new { targetType = "USER", targetId = a.Colleague, note = " Review carefully " };
        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => AssignAsync(client, draft.TaskId, body, key)));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var values = await Task.WhenAll(replies.Select(r => r.Content.ReadFromJsonAsync<TaskAssignedView>())); Assert.Single(values.Distinct());
        Assert.Equal(HttpStatusCode.Conflict, (await AssignAsync(client, draft.TaskId, body with { note = "Changed" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AssignAsync(client, draft.TaskId, body, Guid.NewGuid().ToString())).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(TaskState.Assigned, (await db.WorkTasks.SingleAsync(t => t.Id == draft.TaskId)).Status);
        Assert.Equal(1, await db.TaskAssignments.CountAsync(t => t.TaskId == draft.TaskId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(t => t.ObjectId == draft.TaskId && t.Action == "TASK.ASSIGNED"));
        await using var recipient = database.Create(a.Colleague, a.Tenant);
        Assert.Equal(1, await recipient.Notifications.CountAsync(n => n.ObjectId == draft.TaskId && n.Type == "TaskAssigned"));
        Assert.True(await recipient.WorkTasks.AnyAsync(t => t.Id == draft.TaskId));
        var detail = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{draft.TaskId}"))!;
        Assert.Equal(a.Colleague, detail.Task.AssignedUserId); Assert.Equal("ASSIGNED", detail.Task.Status);
        foreach (var response in replies) response.Dispose();
    }
    [Fact]
    public async Task Scope_and_live_target_checks_deny_missing_foreign_inactive_and_revoked_targets()
    {
        var a = await SeedAsync("MANAGER"); var other = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var draft = (await (await CreateAsync(client, new { title = "Scoped assignment" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var body = new { targetType = "USER", targetId = a.Colleague };
        Assert.Equal(HttpStatusCode.Forbidden, (await AssignAsync(client, draft.TaskId, body)).StatusCode);
        await ScopeAsync(a);
        Assert.Equal(HttpStatusCode.NotFound, (await AssignAsync(client, draft.TaskId, body with { targetId = other.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AssignAsync(client, draft.TaskId, new { targetType = "USER", targetId = a.Colleague, tenantId = other.Tenant })).StatusCode);
        var key = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, body, key)).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        // Eligibility gates new execution; a historical replay does not reassign an inactive target.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={a.Colleague}");
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, body, key)).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ManagementScope\" WHERE \"UserId\"={a.User}");
        Assert.Equal(HttpStatusCode.Forbidden, (await AssignAsync(client, draft.TaskId, body, key)).StatusCode);
    }
    [Fact]
    public async Task Department_assignment_notifies_active_members_and_keeps_the_queue_unclaimed()
    {
        var a = await SeedAsync("MANAGER"); var department = await ScopeAsync(a);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var draft = (await (await CreateAsync(client, new { title = "Team queue" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, new { targetType = "DEPARTMENT", targetId = department })).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        var assignment = await db.TaskAssignments.SingleAsync(t => t.TaskId == draft.TaskId);
        Assert.Null(assignment.UserId); Assert.Equal(department, assignment.DepartmentId);
        Assert.Equal(2, await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == a.Tenant && n.ObjectId == draft.TaskId));
    }
    [Fact]
    public async Task Rejected_history_is_ended_not_overwritten_and_audit_failure_rolls_back_every_effect()
    {
        var a = await SeedAsync("MANAGER"); await ScopeAsync(a);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var draft = (await (await CreateAsync(client, new { title = "Reject and assign" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var body = new { targetType = "USER", targetId = a.Colleague };
        var initial = (await (await AssignAsync(client, draft.TaskId, body)).Content.ReadFromJsonAsync<TaskAssignedView>())!;
        await using var db = database.Create(a.User, a.Tenant); var rejectedAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "TaskAssignment" SET "RejectedAt"={rejectedAt},"RejectionReason"='Cannot undertake' WHERE "TaskAssignmentId"={initial.AssignmentId};
            UPDATE "Task" SET "Status"='REJECTED' WHERE "TaskId"={draft.TaskId};
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_assignment_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='TASK.ASSIGNED' THEN RAISE EXCEPTION 'test assignment audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_assignment_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_assignment_audit_failure();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await AssignAsync(client, draft.TaskId, body)).StatusCode);
            Assert.Equal(TaskState.Rejected, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == draft.TaskId)).Status);
            Assert.Null((await db.TaskAssignments.AsNoTracking().SingleAsync(t => t.TaskId == draft.TaskId)).EndedAt);
            Assert.Equal(1, await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == a.Tenant && n.ObjectId == draft.TaskId));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_assignment_audit_failure ON \"AuditLog\"; DROP FUNCTION test_assignment_audit_failure();"); }
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, body)).StatusCode);
        var history = await db.TaskAssignments.AsNoTracking().Where(t => t.TaskId == draft.TaskId).ToArrayAsync(); Assert.Equal(2, history.Length);
        var old = Assert.Single(history, t => t.Id == initial.AssignmentId); Assert.NotNull(old.EndedAt); Assert.Equal("Cannot undertake", old.RejectionReason);
        Assert.Equal(a.Colleague, old.UserId); Assert.NotNull(old.RejectedAt);
    }
}
