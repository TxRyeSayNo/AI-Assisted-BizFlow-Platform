using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Notifications;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class NotificationInboxTests(PostgresFixture database)
{
    private const string Password = "Notification-test-only!8452";
    private const string Endpoint = "/api/v1/notifications";

    [Fact]
    public async Task Platform_account_has_no_implicit_tenant_recipient_inbox()
    {
        await using var db = database.Create(database.PlatformUserId, null);
        var platform = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(platform, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={platform.Id}");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/read", new { })).StatusCode);
    }

    [Fact]
    public async Task Inbox_counts_and_payloads_are_recipient_scoped_even_for_administrators_and_forged_inputs()
    {
        var a = await SeedAsync(); var b = await SeedAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var colleague = UserAccount.CreateTenantUser(a.TenantId, "OTHER", "other@example.test", "Other", "fixture-only", DateTimeOffset.UtcNow);
        db.Users.Add(colleague); await db.SaveChangesAsync();
        var own = Notification.Create(a.TenantId, a.UserId, NotificationEvent.TaskAssigned, "Own event", "Own content", "own");
        var other = Notification.Create(a.TenantId, colleague.Id, NotificationEvent.TaskAssigned, "Colleague secret", "Private", "other");
        db.Notifications.AddRange(own, other);
        var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        db.UserRoles.Add(new(a.UserId, role.Id)); await db.SaveChangesAsync();
        await using var foreignDb = database.Create(b.UserId, b.TenantId);
        var foreign = Notification.Create(b.TenantId, b.UserId, NotificationEvent.TaskAssigned, "Foreign secret", "Private", "own");
        foreignDb.Notifications.Add(foreign); await foreignDb.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await LoginAsync(client, a); client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        using var response = await client.GetAsync(Endpoint + $"?tenantId={b.TenantId}&recipientId={colleague.Id}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<NotificationPage>())!;
        Assert.Equal(1, page.Total); Assert.Equal(1, page.UnreadCount); Assert.Equal(own.Id, Assert.Single(page.Items).NotificationId);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "content", "notificationId", "objectId", "objectType", "readAt", "sentAt", "title", "type" }, json.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Order());
        foreach (var id in new[] { other.Id, foreign.Id, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"{Endpoint}/{id}/read", new { })).StatusCode);
        Assert.Equal(3, await db.AuditLogs.CountAsync(audit => audit.Action == "SECURITY.ACCESS_DENIED"));
        Assert.Null((await foreignDb.Notifications.SingleAsync()).ReadAt);
    }

    [Fact]
    public async Task Concurrent_read_receipts_are_idempotent_audited_once_and_do_not_claim_email_delivery()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var notification = Notification.Create(account.TenantId, account.UserId, NotificationEvent.RequestResolved, "Resolved", "Review the resolution", "resolution");
        db.Notifications.Add(notification); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PatchAsJsonAsync($"{Endpoint}/{notification.Id}/read", new { })));
        var receipts = new List<DateTimeOffset?>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
                var row = (await response.Content.ReadFromJsonAsync<NotificationRow>())!;
                Assert.NotNull(row.ReadAt); Assert.Null(row.SentAt); receipts.Add(row.ReadAt);
            }
        }
        Assert.Single(receipts.Distinct());
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "NOTIFICATION.READ");
        Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(notification.Id, audit.ObjectId);
        var unread = (await client.GetFromJsonAsync<NotificationPage>(Endpoint + "?unreadOnly=true"))!;
        Assert.Empty(unread.Items); Assert.Equal(0, unread.Total); Assert.Equal(0, unread.UnreadCount);
        Assert.Single((await client.GetFromJsonAsync<NotificationPage>(Endpoint))!.Items);
    }

    [Fact]
    public async Task Paging_validation_and_empty_read_command_do_not_accept_client_timestamps_or_ownership()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var notifications = Enumerable.Range(0, 3).Select(i => Notification.Create(account.TenantId, account.UserId, NotificationEvent.Overdue, "Warning " + i, "Content", "event/" + i)).ToArray();
        db.Notifications.AddRange(notifications); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var first = (await client.GetFromJsonAsync<NotificationPage>(Endpoint + "?pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<NotificationPage>(Endpoint + "?pageSize=1&page=2"))!;
        Assert.Equal(3, first.Total); Assert.Equal(3, first.UnreadCount); Assert.NotEqual(first.Items[0].NotificationId, second.Items[0].NotificationId);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100", "?unreadOnly=invalid" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
        foreach (var body in new object[] { new { readAt = DateTimeOffset.UtcNow }, new { recipientId = account.UserId }, new { tenantId = account.TenantId } })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PatchAsJsonAsync($"{Endpoint}/{notifications[0].Id}/read", body)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync(Endpoint, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(Endpoint)).StatusCode);
        Assert.False(await db.Notifications.AnyAsync(n => n.ReadAt != null));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\"='SUSPENDED' WHERE \"TenantId\"={account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync($"{Endpoint}/{notifications[0].Id}/read", new { })).StatusCode);
    }

    [Fact]
    public async Task Read_receipt_rolls_back_when_audit_persistence_fails()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var notification = Notification.Create(account.TenantId, account.UserId, NotificationEvent.TaskAccepted, "Accepted", "Content", "accepted");
        db.Notifications.Add(notification); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_notification_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action"='NOTIFICATION.READ' THEN RAISE EXCEPTION 'test-only receipt failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_notification_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_notification_audit_failure();
            """);
        try
        {
            await using var factory = new AuthenticationFactory(database);
            using var client = factory.CreateClient(); await LoginAsync(client, account);
            using var response = await client.PatchAsJsonAsync($"{Endpoint}/{notification.Id}/read", new { });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("test-only", await response.Content.ReadAsStringAsync());
            Assert.Null((await db.Notifications.AsNoTracking().SingleAsync()).ReadAt);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "NOTIFICATION.READ"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_notification_audit_failure ON \"AuditLog\"; DROP FUNCTION test_notification_audit_failure();"); }
    }

    private async Task<Account> SeedAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);
        return new(user.Id, tenant.Id, tenant.TenantKey);
    }
    private static async Task LoginAsync(HttpClient client, Account account)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key);
}
