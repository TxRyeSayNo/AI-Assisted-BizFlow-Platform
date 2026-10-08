using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

public sealed partial class TaskListApiTests
{
    private static async Task<HttpResponseMessage> StartAsync(HttpClient client, Guid task, string? key = null, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{task}/start") { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Start_and_overdue_resume_replay_original_results_without_changing_history_or_deadlines()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var initial = await AssignReceiptFixture(client, a, true);
        Assert.Equal(HttpStatusCode.NotFound, (await StartAsync(client, initial.TaskId)).StatusCode); // unclaimed queue
        var accepted = (await (await AcceptAsync(client, initial.TaskId, new { note = "Ready" })).Content.ReadFromJsonAsync<TaskAcceptedView>())!;
        var key = Guid.NewGuid().ToString();
        var replies = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => StartAsync(client, initial.TaskId, key)));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var results = await Task.WhenAll(replies.Select(r => r.Content.ReadFromJsonAsync<TaskStartedView>())); Assert.Single(results.Distinct());
        var started = results[0]!;
        Assert.Equal("IN_PROGRESS", started.Status); Assert.Equal(initial.AssignmentId, started.AssignmentId);
        Assert.Equal(HttpStatusCode.Conflict, (await StartAsync(client, initial.TaskId, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await StartAsync(client, Guid.NewGuid(), key)).StatusCode);
        await using var db = database.Create(a.User, a.Tenant); var deadline = TaskCreationRules.DatabaseTime(DateTimeOffset.UtcNow.AddHours(-1));
        // Simulate scheduler-owned overdue state; this test does not claim the scheduler exists.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='OVERDUE',\"Deadline\"={deadline} WHERE \"TaskId\"={initial.TaskId}");
        Assert.Equal(started, await (await StartAsync(client, initial.TaskId, key)).Content.ReadFromJsonAsync<TaskStartedView>());
        Assert.Equal(TaskState.Overdue, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == initial.TaskId)).Status);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, initial.TaskId, Guid.NewGuid().ToString())).StatusCode);
        var resumed = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == initial.TaskId);
        Assert.Equal(TaskState.InProgress, resumed.Status); Assert.Equal(deadline, resumed.Deadline);
        var receipt = await db.TaskAssignments.SingleAsync(t => t.TaskId == initial.TaskId);
        Assert.Equal(accepted.AcceptedAt, receipt.AcceptedAt); Assert.Equal(initial.AssignedAt, receipt.AssignedAt); Assert.Null(receipt.EndedAt);
        Assert.Equal(1, await db.Confirmations.CountAsync(t => t.ObjectId == initial.TaskId));
        var audits = await db.AuditLogs.Where(t => t.ObjectId == initial.TaskId && t.Action == "TASK.STARTED").ToArrayAsync();
        Assert.Equal(2, audits.Length); Assert.Contains(audits, t => t.BeforeJson!.Contains("OVERDUE"));
        Assert.Empty(await db.TaskProgressReports.Where(t => t.TaskId == initial.TaskId).ToArrayAsync());
        Assert.Empty(await db.TaskResults.Where(t => t.TaskId == initial.TaskId).ToArrayAsync());
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\"={a.User}");
        Assert.Equal(HttpStatusCode.Forbidden, (await StartAsync(client, initial.TaskId, key)).StatusCode);
        foreach (var reply in replies) reply.Dispose();
    }

    [Fact]
    public async Task Start_is_nonrevealing_for_other_targets_and_foreign_tasks_and_rejects_client_state()
    {
        var a = await SeedAsync("MANAGER"); var foreign = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); using var other = factory.CreateClient();
        await LoginAsync(client, a); await LoginAsync(other, foreign);
        var task = await AssignReceiptFixture(client, a, false);
        Assert.Equal(HttpStatusCode.Conflict, (await StartAsync(client, task.TaskId)).StatusCode); // receipt required
        Assert.Equal(HttpStatusCode.NotFound, (await StartAsync(other, task.TaskId)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await StartAsync(client, task.TaskId, body: new { status = "IN_PROGRESS" })).StatusCode);
        var draft = (await (await CreateAsync(client, new { title = "Not assigned to caller" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(client, draft.TaskId, new { targetType = "USER", targetId = a.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await StartAsync(client, draft.TaskId)).StatusCode);
    }

    [Fact]
    public async Task Different_start_keys_have_one_winner_and_audit_failure_rolls_back_state()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var task = await AssignReceiptFixture(client, a, false); Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, task.TaskId, new { })).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_execution_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='TASK.STARTED' THEN RAISE EXCEPTION 'test execution audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_execution_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_execution_audit_failure();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await StartAsync(client, task.TaskId)).StatusCode);
            Assert.Equal(TaskState.Accepted, (await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == task.TaskId)).Status);
            Assert.False(await db.AuditLogs.AnyAsync(t => t.ObjectId == task.TaskId && t.Action == "TASK.STARTED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_execution_audit_failure ON \"AuditLog\"; DROP FUNCTION test_execution_audit_failure();"); }
        var replies = await Task.WhenAll(StartAsync(client, task.TaskId, Guid.NewGuid().ToString()), StartAsync(client, task.TaskId, Guid.NewGuid().ToString()));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await db.AuditLogs.CountAsync(t => t.ObjectId == task.TaskId && t.Action == "TASK.STARTED"));
    }

    [Fact]
    public async Task Execution_migration_cannot_discard_saved_replay_evidence()
    {
        var isolated = new PostgresFixture();
        try
        {
            await isolated.InitializeAsync(); var suite = new TaskListApiTests(isolated); var a = await suite.SeedAsync("MANAGER");
            await using var factory = new AuthenticationFactory(isolated); using var client = factory.CreateClient(); await LoginAsync(client, a);
            var task = await suite.AssignReceiptFixture(client, a, false); Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, task.TaskId, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, task.TaskId, Guid.NewGuid().ToString())).StatusCode);
            await using var db = isolated.Create(a.User, a.Tenant);
            Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261006143521_TaskAcceptanceCommand"))).SqlState);
            Assert.True(await db.AuditLogs.AnyAsync(t => t.ObjectId == task.TaskId && t.Action == "TASK.STARTED"));
            Assert.Contains("20261006191621_TaskExecutionCommand", await db.Database.GetAppliedMigrationsAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }
}
