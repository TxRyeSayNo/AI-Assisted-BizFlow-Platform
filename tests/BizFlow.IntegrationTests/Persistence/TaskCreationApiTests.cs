using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using BizFlow.Domain.Organization;
using Microsoft.AspNetCore.Identity;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Tasks;

namespace BizFlow.IntegrationTests.Persistence;

public sealed partial class TaskListApiTests
{
    [Fact]
    public async Task Detail_histories_are_bounded_ordered_and_never_include_another_tasks_evidence()
    {
        var a = await SeedAsync("EMPLOYEE"); await using var db = database.Create(a.User, a.Tenant);
        var own = await db.WorkTasks.SingleAsync(t => t.Title == "Own");
        var other = await db.WorkTasks.SingleAsync(t => t.Title == "Unrelated"); var now = DateTimeOffset.UtcNow;
        foreach (var task in new[] { own, other })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='IN_PROGRESS' WHERE \"TaskId\"={task.Id}");
            foreach (var revision in Enumerable.Range(1, 3))
            {
                db.TaskProgressReports.Add(TaskProgressReport.Create(task.Id, a.User, (short)(revision * 10), task.Title + " progress", now.AddMinutes(revision)));
                db.TaskResults.Add(TaskResult.CreateSnapshot(task.Id, a.User, task.Title + " result", revision, now.AddMinutes(revision)));
            }
        }
        await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var first = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{own.Id}?pageSize=1"))!;
        Assert.Equal(3, first.Reports.Total); Assert.Equal(3, first.Results.Total);
        Assert.Equal(30, Assert.Single(first.Reports.Items).Percent); Assert.Equal(3, Assert.Single(first.Results.Items).RevisionNo);
        var second = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{own.Id}?pageSize=1&reportPage=2&resultPage=2"))!;
        Assert.Equal("Own progress", Assert.Single(second.Reports.Items).Content);
        Assert.Equal("Own result", Assert.Single(second.Results.Items).Content);
        Assert.Equal(20, second.Reports.Items[0].Percent); Assert.Equal(2, second.Results.Items[0].RevisionNo);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{other.Id}")).StatusCode);
    }

    [Fact]
    public async Task Same_key_from_another_actor_in_the_same_tenant_has_an_independent_result()
    {
        var a = await SeedAsync("MANAGER"); await using var db = database.Create(a.User, a.Tenant);
        var colleague = await db.Users.SingleAsync(u => u.Id == a.Colleague);
        var hash = new PasswordHasher<UserAccount>().HashPassword(colleague, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={colleague.Id}");
        var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");
        db.UserRoles.Add(new(colleague.Id, role.Id)); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var first = factory.CreateClient(); using var second = factory.CreateClient();
        await LoginAsync(first, a);
        using var login = await second.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "COLLEAGUE", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        second.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        var key = Guid.NewGuid().ToString();
        var one = (await (await CreateAsync(first, new { title = "First actor" }, key)).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        var two = (await (await CreateAsync(second, new { title = "Second actor" }, key)).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.NotEqual(one.TaskId, two.TaskId);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"{Endpoint}/{one.TaskId}")).StatusCode);
    }

    private sealed class TaskClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Replay_after_deadline_preserves_result_but_revoked_permission_denies_it()
    {
        var a = await SeedAsync("MANAGER"); var clock = new TaskClock();
        await using var factory = new AuthenticationFactory(database, configureServices: services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        });
        using var client = factory.CreateClient(); await LoginAsync(client, a);
        var key = Guid.NewGuid().ToString(); var deadline = clock.Now.AddMinutes(1).ToString("O"); var input = new { title = "Timed", deadline };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CreateAsync(client, new { title = "Equal", deadline = clock.Now.ToString("O") })).StatusCode);
        var created = (await (await CreateAsync(client, input, key)).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal(created, await (await CreateAsync(client, input, key)).Content.ReadFromJsonAsync<TaskCreatedView>());
        await using var db = database.Create(a.User, a.Tenant);
        var grants = await db.UserRoles.Where(r => r.UserId == a.User).ToArrayAsync(); db.UserRoles.RemoveRange(grants); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(client, input, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{created.TaskId}")).StatusCode);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_task_and_checklist_then_same_key_can_retry()
    {
        var a = await SeedAsync("MANAGER"); await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_task_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='TASK.CREATED' THEN RAISE EXCEPTION 'test-only audit failure'; END IF;
              RETURN NEW; END $$;
            CREATE TRIGGER test_task_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_task_audit_failure();
            """);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var input = new { title = "Atomic failure", checklist = new[] { "Evidence" } }; var key = Guid.NewGuid().ToString();
        try
        {
            var response = await CreateAsync(client, input, key); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("test-only", await response.Content.ReadAsStringAsync());
            Assert.False(await db.WorkTasks.AnyAsync(t => t.Title == input.title));
            Assert.False(await db.TaskChecklistItems.AnyAsync(c => c.Title == "Evidence"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_task_audit_failure ON \"AuditLog\"; DROP FUNCTION test_task_audit_failure();"); }
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(client, input, key)).StatusCode);
    }

    [Fact]
    public async Task Replay_constraint_and_index_are_present_and_populated_downgrade_is_refused()
    {
        var isolated = new PostgresFixture();
        try
        {
        await isolated.InitializeAsync();
        var a = await new TaskListApiTests(isolated).SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(isolated); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var result = (await (await CreateAsync(client, new { title = "Replay guard" }, Guid.NewGuid().ToString())).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        await using var db = isolated.Create(a.User, a.Tenant);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261005055653_TaskCreationPermission"));
        Assert.Contains("replay protection", error.MessageText);
        Assert.Contains("20261006033038_TaskCreationReplayProtection", await db.Database.GetAppliedMigrationsAsync());
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AuditLog" ("AuditLogId","TenantId","ActorType","ActorId","Action","ObjectType","ObjectId","AfterJson","MetadataJson","CreatedAt")
            SELECT {Guid.NewGuid()},"TenantId","ActorType","ActorId","Action","ObjectType","ObjectId","AfterJson","MetadataJson","CreatedAt"
            FROM "AuditLog" WHERE "ObjectId"={result.TaskId} AND "Action"='TASK.CREATED'
            """));
        Assert.Equal("23505", duplicate.SqlState);
        }
        finally { await isolated.DisposeAsync(); }
    }

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Concurrent_creation_replays_once_and_changed_payload_conflicts_with_immutable_audit()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var key = Guid.NewGuid().ToString();
        var deadline = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var input = new { title = "  New work  ", description = " Details ", priority = "HIGH", deadline, checklist = new[] { "First", "Second" } };
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CreateAsync(client, input, key)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<TaskCreatedView>()));
        Assert.Single(results.Distinct()); var created = results[0]!;
        Assert.Equal("New work", created.Title); Assert.Equal("DRAFT", created.Status);
        Assert.Equal($"/api/v1/tasks/{created.TaskId}", responses[0].Headers.Location!.ToString());
        foreach (var changed in new object[] {
            input with { title = "Different" }, input with { priority = "LOW" },
            input with { deadline = DateTimeOffset.UtcNow.AddDays(2).ToString("O") },
            input with { checklist = new[] { "Second", "First" } } })
            Assert.Equal(HttpStatusCode.Conflict, (await CreateAsync(client, changed, key)).StatusCode);
        var normalized = input with { title = "New work", description = "Details", deadline = DateTimeOffset.Parse(deadline).ToOffset(TimeSpan.FromHours(7)).ToString("O") };
        Assert.Equal(created, await (await CreateAsync(client, normalized, key)).Content.ReadFromJsonAsync<TaskCreatedView>());
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(1, await db.WorkTasks.CountAsync(t => t.Title == "New work"));
        Assert.Equal(2, await db.TaskChecklistItems.CountAsync(c => c.TaskId == created.TaskId));
        var audit = await db.AuditLogs.SingleAsync(x => x.Action == "TASK.CREATED" && x.ObjectId == created.TaskId);
        Assert.DoesNotContain(key, audit.MetadataJson!); Assert.Contains(TaskCreationRules.KeyHash(key)!, audit.MetadataJson!);
        var detail = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{created.TaskId}?pageSize=1"))!;
        Assert.Equal("Details", detail.Description); Assert.Equal(2, detail.Checklist.Total);
        Assert.Equal(TaskCreationRules.ParseDeadline(deadline), detail.Task.Deadline);
        Assert.Equal(TimeSpan.Zero, detail.Task.Deadline!.Value.Offset);
        Assert.Equal("First", Assert.Single(detail.Checklist.Items).Title); Assert.Empty(detail.Results.Items);
        var second = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{created.TaskId}?pageSize=1&checklistPage=2"))!;
        Assert.Equal("Second", Assert.Single(second.Checklist.Items).Title);
        // Replay is the original result, not the current mutable task projection.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Title\"='Later title' WHERE \"TaskId\"={created.TaskId}");
        Assert.Equal(created, await (await CreateAsync(client, input, key)).Content.ReadFromJsonAsync<TaskCreatedView>());
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Concurrent_different_payload_has_one_winner_and_keys_are_tenant_isolated()
    {
        var a = await SeedAsync("MANAGER"); var b = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); using var other = factory.CreateClient();
        await LoginAsync(client, a); await LoginAsync(other, b); var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(CreateAsync(client, new { title = "A" }, key), CreateAsync(client, new { title = "B" }, key));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(other, new { title = "Independent tenant" }, key)).StatusCode);
        var created = (await results.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Endpoint}/{created.TaskId}")).StatusCode);
    }

    [Fact]
    public async Task Creation_rejects_untrusted_fields_invalid_deadlines_and_unauthorized_actors()
    {
        var a = await SeedAsync("MANAGER"); var employee = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateAsync(client, new { title = "No" })).StatusCode);
        await LoginAsync(client, employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(client, new { title = "No" })).StatusCode);
        await LoginAsync(client, a);
        foreach (var body in new object[] { new { title = "X", tenantId = a.Tenant }, new { title = "X", status = "COMPLETED" },
            new { title = "X", deadline = "2030-01-01T08:00" }, new { title = "X", deadline = "2020-01-01T00:00:00Z" },
            new { title = "" }, new { title = "X", checklist = new[] { "" } } })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CreateAsync(client, body)).StatusCode);
        var created = (await (await CreateAsync(client, new { title = "No deadline" })).Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Null((await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{created.TaskId}"))!.Task.Deadline);
    }

    [Theory]
    [InlineData("EMPLOYEE")]
    [InlineData("MANAGER")]
    [InlineData("COMPANY_ADMIN")]
    public async Task Detail_matches_list_scope_and_denials_are_nonrevealing(string role)
    {
        var a = await SeedAsync(role);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Endpoint}/{Guid.NewGuid()}")).StatusCode);
        await LoginAsync(client, a);
        var visible = (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!.Items.Select(t => t.TaskId).ToHashSet();
        await using var db = database.Create(a.User, a.Tenant);
        foreach (var id in await db.WorkTasks.Select(t => t.Id).ToArrayAsync())
            Assert.Equal(visible.Contains(id) ? HttpStatusCode.OK : HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{Endpoint}/{visible.First()}?pageSize=101")).StatusCode);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "SECURITY.ACCESS_DENIED"));
    }
}
