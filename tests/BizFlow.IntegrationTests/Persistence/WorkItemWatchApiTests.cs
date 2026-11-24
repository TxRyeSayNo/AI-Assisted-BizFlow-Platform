using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Collaboration;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for watchers and "@" mentions (F3): the new tables against PostgreSQL, the
/// read-policy boundary that stops a mention from disclosing an item, tenant isolation, the
/// Mentions feature flag, and the idempotent activity sweep that reads the existing audit trail.
/// </summary>
[Collection("Postgres")]
public sealed class WorkItemWatchApiTests(PostgresFixture database)
{
    private const string Password = "Watcher-test!6621";

    private static string Watch(Guid taskId) => $"/api/v1/work-items/TASK/{taskId}/watch";
    private static string Mentions(Guid taskId) => $"/api/v1/work-items/TASK/{taskId}/mentions";

    [Fact]
    public async Task Following_and_unfollowing_are_idempotent_and_report_the_watcher_count()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var initial = (await client.GetFromJsonAsync<WorkItemWatchStatus>(Watch(a.TaskId)))!;
        Assert.False(initial.Watching);
        Assert.Equal(0, initial.WatcherCount);
        Assert.Equal("TASK", initial.ObjectType);

        var watched = await PutWatchAsync(client, a.TaskId, factory);
        Assert.True(watched.Watching);
        Assert.Equal(1, watched.WatcherCount);

        // Pressing follow twice is not an error and does not double count.
        var again = await PutWatchAsync(client, a.TaskId, factory);
        Assert.True(again.Watching);
        Assert.Equal(1, again.WatcherCount);

        Assert.False((await DeleteWatchAsync(client, a.TaskId, factory)).Watching);
        // And so is unfollowing something you do not follow.
        Assert.False((await DeleteWatchAsync(client, a.TaskId, factory)).Watching);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task An_item_outside_the_callers_read_scope_cannot_be_watched_or_mentioned_on()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // The employee holds no assignment on the colleague-only task and does not manage it.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Watch(a.ForeignScopeTaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync(Watch(a.ForeignScopeTaskId), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Watch(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task One_tenant_can_never_watch_another_tenants_item()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Watch(foreign.TaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(Watch(foreign.TaskId), new { })).StatusCode);

        // Nothing was written for the other tenant.
        await using var db = database.Create(foreign.User, foreign.Tenant);
        Assert.Empty(await db.WorkItemWatchers.Where(w => w.ObjectId == foreign.TaskId).ToListAsync());
    }

    [Fact]
    public async Task A_mention_notifies_only_people_who_could_already_open_the_item()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var commentId = await CommentAsync(client, a.TaskId, "Nhờ @Đồng nghiệp xem lại giúp.");

        var result = await MentionAsync(client, a.TaskId, commentId, factory, a.Colleague, a.Stranger);

        // The colleague is assigned to the task; the stranger has no path to it at all.
        Assert.Equal(1, result.Recorded);
        Assert.Equal(a.Stranger, Assert.Single(result.Skipped));

        await using var db = database.Create(a.User, a.Tenant);
        var mention = Assert.Single(await db.CommentMentions.ToListAsync());
        Assert.Equal(a.Colleague, mention.MentionedUserId);
        Assert.Equal(a.User, mention.MentionedByUserId);
        // Notifications are filtered to their own recipient, so this must be read as the colleague.
        await using var inbox = database.Create(a.Colleague, a.Tenant);
        var notification = Assert.Single(await inbox.Notifications
            .Where(n => n.Type == "Mentioned").ToListAsync());
        Assert.Equal(a.Colleague, notification.RecipientId);
        Assert.Equal("TASK", notification.ObjectType);
        Assert.Equal(a.TaskId, notification.ObjectId);
        Assert.Contains("Nhờ", notification.Content);

        // Replaying the same mention records nobody twice.
        var replay = await MentionAsync(client, a.TaskId, commentId, factory, a.Colleague);
        Assert.Equal(0, replay.Recorded);
        Assert.Single(await db.CommentMentions.ToListAsync());
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task Mention_input_is_validated_and_only_the_comment_author_may_record_it()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var commentId = await CommentAsync(client, a.TaskId, "Ghi chú");

        // Mentioning nobody, or only yourself, is a validation failure rather than a silent no-op.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Mentions(a.TaskId),
            new { commentId, userIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Mentions(a.TaskId),
            new { commentId, userIds = new[] { a.User } })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Mentions(a.TaskId),
            new { commentId, userIds = Enumerable.Range(0, WorkItemWatchService.MaxMentionsPerComment + 1).Select(_ => Guid.NewGuid()).ToArray() })).StatusCode);
        // A comment that belongs to another object is not addressable here.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Mentions(a.TaskId),
            new { commentId = Guid.NewGuid(), userIds = new[] { a.Colleague } })).StatusCode);
        // An unknown work item type never reaches storage.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"/api/v1/work-items/PROJECT/{a.TaskId}/watch")).StatusCode);
    }

    [Fact]
    public async Task The_activity_sweep_reports_audit_and_comment_history_once_and_only_once()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        await PutWatchAsync(client, a.TaskId);

        // The notifier is scoped, exactly as it is when Hangfire activates it inside a job scope.
        using var scope = factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IWatchedItemActivityNotifier>();

        // Nothing has happened since the subscription, so a sweep is silent.
        Assert.Equal(0, await notifier.SweepTenantAsync(a.Tenant, default));

        // Someone else comments and the audit trail records a change.
        await using (var db = database.Create(a.Colleague, a.Tenant))
        {
            db.Comments.Add(Comment.Create(a.Tenant, CommentObjectType.Task, a.TaskId, a.Colleague,
                "Đồng nghiệp bổ sung thông tin", DateTimeOffset.UtcNow.AddSeconds(2)));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AuditLog" ("AuditLogId", "TenantId", "ActorType", "ActorId", "Action",
                    "ObjectType", "ObjectId", "CreatedAt")
                VALUES ({Guid.CreateVersion7()}, {a.Tenant}, 'USER', {a.Colleague}, 'TASK_STARTED',
                    'TASK', {a.TaskId}, {DateTimeOffset.UtcNow.AddSeconds(3)});
                """);
        }

        Assert.Equal(1, await notifier.SweepTenantAsync(a.Tenant, default));
        // Running it again must not notify the same activity twice.
        Assert.Equal(0, await notifier.SweepTenantAsync(a.Tenant, default));

        await using var verify = database.Create(a.User, a.Tenant);
        var notification = Assert.Single(await verify.Notifications
            .Where(n => n.Type == "WatchedItemActivity").ToListAsync());
        Assert.Equal(a.User, notification.RecipientId);
        Assert.Equal(a.TaskId, notification.ObjectId);
        Assert.Contains("bình luận mới", notification.Content);
        Assert.Contains("TASK_STARTED", notification.Content);
    }

    [Fact]
    public async Task The_sweep_never_tells_a_watcher_about_their_own_activity()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        await PutWatchAsync(client, a.TaskId);

        await using (var db = database.Create(a.User, a.Tenant))
        {
            db.Comments.Add(Comment.Create(a.Tenant, CommentObjectType.Task, a.TaskId, a.User,
                "Tự ghi chú", DateTimeOffset.UtcNow.AddSeconds(2)));
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IWatchedItemActivityNotifier>();
        Assert.Equal(0, await notifier.SweepTenantAsync(a.Tenant, default));
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_every_watcher_endpoint_without_touching_comments()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:Mentions"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Watch(a.TaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(Watch(a.TaskId), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(Watch(a.TaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Mentions(a.TaskId),
            new { commentId = Guid.NewGuid(), userIds = new[] { a.Colleague } })).StatusCode);

        // The pre-existing comment endpoints keep working exactly as before.
        using var comment = await client.PostAsJsonAsync($"/api/v1/tasks/{a.TaskId}/comments",
            new { content = "Vẫn bình luận được" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
    }

    [Fact]
    public async Task Watcher_endpoints_require_authentication_and_are_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Watch(a.TaskId))).StatusCode);

        await LoginAsync(client, a);
        using var response = await client.GetAsync(Watch(a.TaskId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

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
        // Holds no assignment and no management scope anywhere: the mention must skip them.
        var stranger = UserAccount.CreateTenantUser(tenant.Id, "STRANGER", "stranger@example.test",
            "Người ngoài phạm vi", "fixture-only", now, other.Id);
        db.AddRange(colleague, stranger);
        await db.SaveChangesAsync();

        var task = WorkTask.CreateDraft(tenant.Id, user.Id, "Nhiệm vụ được theo dõi", now);
        var foreignScope = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Nhiệm vụ của người khác", now);
        db.AddRange(task, foreignScope);
        await db.SaveChangesAsync();

        db.AddRange(
            TaskAssignment.Create(task.Id, user.Id, null, colleague.Id, now),
            TaskAssignment.Create(foreignScope.Id, colleague.Id, null, colleague.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={ours.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            var colleagueRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
            db.UserRoles.Add(new(colleague.Id, colleagueRole.Id));
            db.UserRoles.Add(new(stranger.Id, colleagueRole.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id, stranger.Id, task.Id, foreignScope.Id);
    }

    /// <summary>Asserts the call actually succeeded, so an error envelope can never be mistaken
    /// for a status object full of default values.</summary>
    private static async Task<WorkItemWatchStatus> PutWatchAsync(HttpClient client, Guid taskId,
        AuthenticationFactory? factory = null)
    {
        using var response = await client.PutAsJsonAsync(Watch(taskId), new { });
        // Surface the server exception itself rather than a bare 500 when something breaks.
        if (factory is not null) Assert.Null(factory.LastServerError);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkItemWatchStatus>())!;
    }

    private static async Task<WorkItemWatchStatus> DeleteWatchAsync(HttpClient client, Guid taskId,
        AuthenticationFactory factory)
    {
        using var response = await client.DeleteAsync(Watch(taskId));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Null(factory.LastServerError);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        return System.Text.Json.JsonSerializer.Deserialize<WorkItemWatchStatus>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    private static async Task<Guid> CommentAsync(HttpClient client, Guid taskId, string content)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments", new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CommentProbe>())!;
        Assert.NotEqual(Guid.Empty, created.CommentId);
        return created.CommentId;
    }

    private static async Task<WorkItemMentionResult> MentionAsync(HttpClient client, Guid taskId,
        Guid commentId, AuthenticationFactory? factory, params Guid[] userIds)
    {
        using var response = await client.PostAsJsonAsync(Mentions(taskId), new { commentId, userIds });
        if (factory is not null) Assert.Null(factory.LastServerError);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkItemMentionResult>())!;
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record CommentProbe(Guid CommentId);

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Colleague, Guid Stranger,
        Guid TaskId, Guid ForeignScopeTaskId);
}
