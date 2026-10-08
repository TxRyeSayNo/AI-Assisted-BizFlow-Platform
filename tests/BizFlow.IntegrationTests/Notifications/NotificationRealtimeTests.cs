using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using BizFlow.Api.Realtime;
using BizFlow.Application.Authentication;
using BizFlow.Application.Notifications;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class NotificationRealtimeTests(PostgresFixture database)
{
    private const string Password = "Realtime-test-only!8452";

    [Fact]
    public async Task Query_credentials_are_limited_to_exact_transport_and_join_scope_is_server_selected()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(NotificationHub.Path + "/negotiate?negotiateVersion=1", null)).StatusCode);
        var token = await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications?access_token=" + token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(NotificationHub.Path + "/negotiate?negotiateVersion=1&access_token=" + token, null)).StatusCode);
        using var socket = await ConnectAsync(factory, token, "&tenantId=" + Guid.NewGuid() + "&recipientId=" + Guid.NewGuid());
        var registry = factory.Services.GetRequiredService<NotificationConnections>();
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId]).Length == 1);
        Assert.Single(registry.For(account.TenantId, [account.UserId]));
        await PublishAsync(factory, account.TenantId, account.UserId);
        Assert.Equal("{\"type\":1,\"target\":\"InboxChanged\",\"arguments\":[]}\u001e", await ReceiveAsync(socket));
    }

    [Fact]
    public async Task Receipt_commit_notifies_only_its_recipient_not_colleagues_or_other_tenants()
    {
        var account = await SeedAsync(); var foreign = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var colleague = UserAccount.CreateTenantUser(account.TenantId, "COLLEAGUE", "colleague@example.test", "Colleague", "fixture", DateTimeOffset.UtcNow);
        db.Users.Add(colleague); await db.SaveChangesAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(colleague, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={colleague.Id}");
        var notice = Notification.Create(account.TenantId, account.UserId, NotificationEvent.TaskAssigned, "Assigned", "Private work", "realtime-test");
        db.Notifications.Add(notice); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, account);
        using var ownSocket = await ConnectAsync(factory, token);
        using var otherSocket = await ConnectAsync(factory, await LoginAsync(client, new(colleague.Id, account.TenantId, account.Key, "COLLEAGUE")));
        using var foreignSocket = await ConnectAsync(factory, await LoginAsync(client, foreign));
        var registry = factory.Services.GetRequiredService<NotificationConnections>();
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId, colleague.Id]).Length == 2 && registry.For(foreign.TenantId, [foreign.UserId]).Length == 1);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/notifications/{notice.Id}/read", new { })).StatusCode);
        Assert.Contains("InboxChanged", await ReceiveAsync(ownSocket));
        Assert.NotNull((await db.Notifications.AsNoTracking().SingleAsync()).ReadAt);
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "NOTIFICATION.READ").ToListAsync());
        // Idempotent receipt replay has no additional mutation or hint.
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/notifications/{notice.Id}/read", new { })).StatusCode);
        await AssertSilentAsync(ownSocket); await AssertSilentAsync(otherSocket); await AssertSilentAsync(foreignSocket);
    }

    [Fact]
    public async Task Revoked_session_is_removed_before_delivery_but_another_live_session_still_receives()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var revokedSocket = await ConnectAsync(factory, await LoginAsync(client, account));
        var registry = factory.Services.GetRequiredService<NotificationConnections>();
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId]).Length == 1);
        var revokedIdentity = registry.For(account.TenantId, [account.UserId]).Single().Value;
        using var activeSocket = await ConnectAsync(factory, await LoginAsync(client, account));
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId]).Length == 2);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AuthenticationSession\" SET \"RevokedAt\"={DateTimeOffset.UtcNow} WHERE \"FamilyId\"={revokedIdentity.FamilyId}");
        await PublishAsync(factory, account.TenantId, account.UserId);
        Assert.Contains("InboxChanged", await ReceiveAsync(activeSocket));
        Assert.Single(registry.For(account.TenantId, [account.UserId]));
        await AssertSilentAsync(revokedSocket);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\"='SUSPENDED' WHERE \"TenantId\"={account.TenantId}");
        await PublishAsync(factory, account.TenantId, account.UserId);
        Assert.Empty(registry.For(account.TenantId, [account.UserId]));
        await AssertSilentAsync(activeSocket);
    }

    [Fact]
    public async Task Access_token_expiry_closes_the_connection_and_removes_its_subscription()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var initial = await ConnectAsync(factory, await LoginAsync(client, account));
        var registry = factory.Services.GetRequiredService<NotificationConnections>();
        var identity = registry.For(account.TenantId, [account.UserId]).Single().Value;
        var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
        // Test-only signed token with the same valid session, expiring in a few seconds.
        var expiring = issuer.Issue(identity, DateTimeOffset.UtcNow.AddMinutes(-15).AddSeconds(3));
        using var socket = await ConnectAsync(factory, expiring.Secret);
        Assert.Equal(2, registry.For(account.TenantId, [account.UserId]).Length);
        var terminal = await ReceiveAsync(socket, 10000);
        Assert.True(terminal.Length == 0 || terminal.Contains("\"type\":7"));
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId]).Length == 1);
    }

    [Fact]
    public async Task Failed_audit_transaction_never_emits_a_realtime_hint()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var notice = Notification.Create(account.TenantId, account.UserId, NotificationEvent.TaskAccepted, "Accepted", "Private", "rollback-realtime");
        db.Notifications.Add(notice); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_realtime_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='NOTIFICATION.READ' THEN RAISE EXCEPTION 'test-only failure' USING ERRCODE='23514'; END IF;
              RETURN NEW; END $$;
            CREATE TRIGGER test_realtime_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_realtime_audit_failure();
            """);
        try
        {
            await using var factory = new AuthenticationFactory(database);
            using var client = factory.CreateClient(); var token = await LoginAsync(client, account);
            using var socket = await ConnectAsync(factory, token);
            await WaitAsync(() => factory.Services.GetRequiredService<NotificationConnections>().For(account.TenantId, [account.UserId]).Length == 1);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PatchAsJsonAsync($"/api/v1/notifications/{notice.Id}/read", new { })).StatusCode);
            Assert.Null((await db.Notifications.AsNoTracking().SingleAsync()).ReadAt);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "NOTIFICATION.READ"));
            await AssertSilentAsync(socket);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_realtime_audit_failure ON \"AuditLog\"; DROP FUNCTION test_realtime_audit_failure();"); }
    }

    [Fact]
    public async Task Platform_session_cannot_join_a_tenant_inbox()
    {
        await using var db = database.Create(database.PlatformUserId, null);
        var platform = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(platform, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={platform.Id}");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken;
        using var socket = await ConnectAsync(factory, token, "&tenantId=" + Guid.NewGuid(), expectReady: false);
        // After handshake, OnConnected aborts this non-tenant connection, rather than trusting query scope.
        var terminal = await ReceiveAsync(socket);
        Assert.DoesNotContain("InboxChanged", terminal);
        Assert.True(terminal.Length == 0 || terminal.Contains("\"type\":7"));
        Assert.Empty(factory.Services.GetRequiredService<NotificationConnections>().For(Guid.NewGuid(), [platform.Id]));
    }

    [Fact]
    public async Task Transport_failure_after_commit_does_not_fail_or_undo_the_receipt()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var notice = Notification.Create(account.TenantId, account.UserId, NotificationEvent.TaskAccepted, "Accepted", "Private", "transport-failure");
        db.Notifications.Add(notice); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database, configureServices: services => services.AddSingleton<IHubContext<NotificationHub>, BrokenHubContext>());
        using var client = factory.CreateClient(); var token = await LoginAsync(client, account);
        using var socket = await ConnectAsync(factory, token);
        var registry = factory.Services.GetRequiredService<NotificationConnections>();
        await WaitAsync(() => registry.For(account.TenantId, [account.UserId]).Length == 1);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/notifications/{notice.Id}/read", new { })).StatusCode);
        Assert.NotNull((await db.Notifications.AsNoTracking().SingleAsync()).ReadAt);
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "NOTIFICATION.READ").ToListAsync());
    }

    private sealed class BrokenHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients => throw new InvalidOperationException("Simulated transport outage");
        public IGroupManager Groups => throw new NotSupportedException();
    }
    private static async Task PublishAsync(AuthenticationFactory factory, Guid tenant, Guid user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INotificationUpdates>().PublishAsync(tenant, [user], default);
    }
    private static async Task<WebSocket> ConnectAsync(AuthenticationFactory factory, string token, string extraQuery = "", bool expectReady = true)
    {
        var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost" + NotificationHub.Path + "?access_token=" + token + extraQuery), default);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, default);
        Assert.Equal("{}\u001e", await ReceiveAsync(socket));
        if (expectReady) Assert.Equal("{\"type\":1,\"target\":\"InboxReady\",\"arguments\":[]}\u001e", await ReceiveAsync(socket));
        return socket;
    }
    private static async Task<string> ReceiveAsync(WebSocket socket, int timeoutMs = 3000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        var buffer = new byte[4096]; var frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        return Encoding.UTF8.GetString(buffer, 0, frame.Count);
    }
    private static async Task AssertSilentAsync(WebSocket socket) =>
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReceiveAsync(socket, 150));
    private static async Task WaitAsync(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(3000);
        while (!ready()) await Task.Delay(10, timeout.Token);
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
        return new(user.Id, tenant.Id, tenant.TenantKey, "EMP001");
    }
    private static async Task<string> LoginAsync(HttpClient client, Account account)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = account.Code, password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken;
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key, string Code);
}
