using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

public sealed partial class TaskListApiTests
{
    [Fact]
    public async Task Receipt_uses_current_membership_and_cannot_accept_someone_elses_direct_assignment()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var queued = await AssignReceiptFixture(client, a, true);
        await using var db = database.Create(a.User, a.Tenant);
        var originalDepartment = (await db.Users.SingleAsync(u => u.Id == a.User)).DepartmentId;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"={a.OtherDepartment} WHERE \"UserId\"={a.User}");
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(client, queued.TaskId, new { })).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"={originalDepartment} WHERE \"UserId\"={a.User}");
        // Existing responsibility is independent of eligibility for a new department assignment.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Department\" SET \"Status\"='INACTIVE' WHERE \"DepartmentId\"={originalDepartment}");
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, queued.TaskId, new { })).StatusCode);
        var draft = (await (await CreateAsync(client, new { title = "Another target" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, new { targetType = "USER", targetId = a.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(client, draft.TaskId, new { })).StatusCode);
    }

    [Fact]
    public async Task Confirmation_migration_is_tenant_guarded_and_populated_downgrade_preserves_history()
    {
        var isolated = new PostgresFixture();
        try
        {
            await isolated.InitializeAsync(); var suite = new TaskListApiTests(isolated); var a = await suite.SeedAsync("MANAGER"); var foreign = await suite.SeedAsync("EMPLOYEE");
            await using var factory = new AuthenticationFactory(isolated); using var client = factory.CreateClient(); await LoginAsync(client, a);
            var assigned = await suite.AssignReceiptFixture(client, a, false);
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, assigned.TaskId, new { })).StatusCode);
            await using var db = isolated.Create(a.User, a.Tenant);
            var confirmation = await db.Confirmations.SingleAsync(c => c.ObjectId == assigned.TaskId);
            var forged = Guid.CreateVersion7();
            var mismatch = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Confirmation" ("ConfirmationId","TenantId","ObjectType","ObjectId","MilestoneType","ActorId","Decision","ConfirmedAt")
                VALUES ({forged},{foreign.Tenant},'TASK',{assigned.TaskId},'RECEIVE',{foreign.User},'CONFIRMED',{DateTimeOffset.UtcNow})
                """));
            Assert.Equal("23514", mismatch.SqlState);
            Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261006092254_TaskAssignmentCommand"))).SqlState);
            Assert.True(await db.Confirmations.AnyAsync(c => c.Id == confirmation.Id));
            Assert.Contains("20261006143521_TaskAcceptanceCommand", await db.Database.GetAppliedMigrationsAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }

    private static async Task<HttpResponseMessage> AcceptAsync(HttpClient client, Guid task, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{task}/accept") { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private async Task<TaskAssignedView> AssignReceiptFixture(HttpClient client, Account a, bool queue)
    {
        var department = await ScopeAsync(a);
        var draft = (await (await CreateAsync(client, new { title = "Acceptance work" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var response = await AssignAsync(client, draft.TaskId, new { targetType = queue ? "DEPARTMENT" : "USER", targetId = queue ? department : a.User });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TaskAssignedView>())!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acceptance_claim_confirmation_audit_and_notification_are_atomic_and_replay_once(bool queue)
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var initial = await AssignReceiptFixture(client, a, queue); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AcceptAsync(client, initial.TaskId, new { note = " Ready " }, key)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<TaskAcceptedView>())); Assert.Single(results.Distinct());
        var result = results[0]!;
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptAsync(client, initial.TaskId, new { note = "Changed" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptAsync(client, initial.TaskId, new { note = "Ready" }, Guid.NewGuid().ToString())).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        var assignment = await db.TaskAssignments.SingleAsync(t => t.TaskId == initial.TaskId);
        Assert.Equal(a.User, assignment.UserId); Assert.Equal(initial.AssignedAt, assignment.AssignedAt); Assert.Equal(a.User, assignment.AssignedBy);
        Assert.Equal(queue, assignment.DepartmentId is not null); Assert.Equal(result.AcceptedAt, assignment.AcceptedAt);
        Assert.Equal("ACCEPTED", (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{initial.TaskId}"))!.Task.Status);
        var confirmation = await db.Confirmations.SingleAsync(c => c.ObjectId == initial.TaskId);
        Assert.Equal(a.User, confirmation.ActorId); Assert.Equal(result.ConfirmationId, confirmation.Id); Assert.Equal("Ready", confirmation.Note);
        Assert.Equal(result.AcceptedAt, confirmation.ConfirmedAt); Assert.Equal("RECEIVE", confirmation.MilestoneType);
        Assert.Equal(1, await db.AuditLogs.CountAsync(c => c.ObjectId == initial.TaskId && c.Action == "TASK.ACCEPTED"));
        Assert.Equal(1, await db.Notifications.CountAsync(c => c.ObjectId == initial.TaskId && c.Type == "TaskAccepted"));
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Confirmation\" SET \"Note\"='overwrite' WHERE \"ConfirmationId\"={confirmation.Id}"))).SqlState);
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Confirmation\" WHERE \"ConfirmationId\"={confirmation.Id}"))).SqlState);
        var foreign = await SeedAsync("EMPLOYEE"); await using var other = database.Create(foreign.User, foreign.Tenant);
        Assert.False(await other.Confirmations.AnyAsync(c => c.Id == confirmation.Id));
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\"={a.User}");
        Assert.Equal(HttpStatusCode.Forbidden, (await AcceptAsync(client, initial.TaskId, new { note = "Ready" }, key)).StatusCode);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Two_department_members_cannot_claim_one_assignment_and_non_targets_are_hidden()
    {
        var a = await SeedAsync("MANAGER"); var foreign = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database); using var first = factory.CreateClient(); using var second = factory.CreateClient(); using var other = factory.CreateClient();
        await LoginAsync(first, a); await LoginAsync(other, foreign);
        var initial = await AssignReceiptFixture(first, a, true);
        await using var db = database.Create(a.User, a.Tenant);
        var colleague = await db.Users.SingleAsync(u => u.Id == a.Colleague);
        var hash = new PasswordHasher<UserAccount>().HashPassword(colleague, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={a.Colleague}");
        db.UserRoles.Add(new(a.Colleague, (await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE")).Id)); await db.SaveChangesAsync();
        using var login = await second.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "COLLEAGUE", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        second.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(other, initial.TaskId, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AcceptAsync(first, initial.TaskId, new { tenantId = foreign.Tenant })).StatusCode);
        var results = await Task.WhenAll(AcceptAsync(first, initial.TaskId, new { }, Guid.NewGuid().ToString()), AcceptAsync(second, initial.TaskId, new { }, Guid.NewGuid().ToString()));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.NotFound);
        var assignment = await db.TaskAssignments.SingleAsync(t => t.TaskId == initial.TaskId);
        Assert.Contains(assignment.UserId, new Guid?[] { a.User, a.Colleague });
        Assert.Equal(1, await db.Confirmations.CountAsync(c => c.ObjectId == initial.TaskId));
        Assert.Equal(1, await db.Notifications.CountAsync(c => c.ObjectId == initial.TaskId && c.Type == "TaskAccepted"));
    }

    [Fact]
    public async Task Acceptance_audit_failure_rolls_back_claim_state_confirmation_and_notification()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var initial = await AssignReceiptFixture(client, a, true);
        await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_acceptance_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='TASK.ACCEPTED' THEN RAISE EXCEPTION 'test acceptance audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_acceptance_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_acceptance_audit_failure();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await AcceptAsync(client, initial.TaskId, new { })).StatusCode);
            var unchanged = await db.TaskAssignments.AsNoTracking().SingleAsync(t => t.TaskId == initial.TaskId);
            Assert.Null(unchanged.UserId); Assert.Null(unchanged.AcceptedAt);
            Assert.False(await db.Confirmations.AnyAsync(c => c.ObjectId == initial.TaskId));
            Assert.False(await db.Notifications.AnyAsync(c => c.ObjectId == initial.TaskId && c.Type == "TaskAccepted"));
            Assert.Equal("ASSIGNED", (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{initial.TaskId}"))!.Task.Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_acceptance_audit_failure ON \"AuditLog\"; DROP FUNCTION test_acceptance_audit_failure();"); }
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, initial.TaskId, new { })).StatusCode);
    }
}
