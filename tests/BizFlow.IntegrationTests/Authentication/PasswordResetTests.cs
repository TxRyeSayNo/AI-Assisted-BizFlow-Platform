using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizFlow.IntegrationTests.Authentication;

[Collection("Postgres")]
public sealed class PasswordResetTests(PostgresFixture database)
{
    private const string OldPassword = "Old-test-password!123";
    private const string NewPassword = "New-test-passphrase!456";

    [Fact]
    public async Task Reset_is_single_use_invalidates_old_password_and_revokes_every_session()
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var first = await LoginAsync(client, account, OldPassword);
        var second = await LoginAsync(client, account, OldPassword);
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var reset = await ResetAsync(client, account, token, NewPassword);
        Assert.True(reset.StatusCode == HttpStatusCode.OK, factory.LastServerError?.ToString());
        Assert.True(reset.Headers.CacheControl?.NoStore);
        using var replay = await ResetAsync(client, account, token, "Another-new-password!789");
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = OldPassword, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        foreach (var session in new[] { first, second })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
            using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        var newLogin = await LoginAsync(client, account, NewPassword);
        Assert.Equal(account.UserId, newLogin.Session.UserId);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "AUTH.PASSWORD_RESET_SUCCEEDED"));
        Assert.NotEqual(account.InitialStamp, (await db.Users.SingleAsync()).SecurityStamp);
    }

    [Fact]
    public async Task Wrong_workspace_identifier_and_password_policy_do_not_consume_valid_token()
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        using var wrongTenant = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        { identifier = "EMP001", tenantKey = "different-company", resetToken = token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTenant.StatusCode);
        using var wrongUser = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        { identifier = "someone-else", tenantKey = account.Key, resetToken = token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongUser.StatusCode);
        using var weak = await ResetAsync(client, account, token, "weak");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.DoesNotContain("weak", await weak.Content.ReadAsStringAsync());
        using var valid = await ResetAsync(client, account, token, NewPassword);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task Concurrent_reset_consumes_the_security_stamp_once()
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        var results = await Task.WhenAll(ResetAsync(client, account, token, NewPassword), ResetAsync(client, account, token, NewPassword));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        foreach (var result in results) result.Dispose();
    }

    [Fact]
    public async Task Unknown_account_receives_same_acknowledgement_and_no_email()
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        using var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { identifier = "EMP001", tenantKey = account.Key });
        using var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { identifier = "missing", tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        foreach (var item in queue.Items)
            await scope.ServiceProvider.GetRequiredService<PasswordResetDeliveryService>().DeliverAsync(item.Identifier, item.Key, item.Time, default);
        Assert.Single(email.Messages);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("deleted")]
    [InlineData("suspended")]
    [InlineData("email-changed")]
    public async Task Reset_rechecks_account_and_workspace_after_link_was_issued(string change)
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        await using var db = database.Create(account.UserId, account.TenantId);
        switch (change)
        {
            case "inactive": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {account.UserId}"); break;
            case "locked": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'LOCKED' WHERE \"UserId\" = {account.UserId}"); break;
            case "deleted": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DeletedAt\" = now() WHERE \"UserId\" = {account.UserId}"); break;
            case "suspended": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\" = 'SUSPENDED' WHERE \"TenantId\" = {account.TenantId}"); break;
            case "email-changed": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Email\" = 'changed@example.test', \"NormalizedEmail\" = 'CHANGED@EXAMPLE.TEST' WHERE \"UserId\" = {account.UserId}"); break;
        }
        using var response = await ResetAsync(client, account, token, NewPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(account.InitialStamp, (await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == account.UserId)).SecurityStamp);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "AUTH.PASSWORD_RESET_SUCCEEDED"));
    }

    [Fact]
    public async Task Email_reset_recovers_operational_lockout_without_unlocking_business_status()
    {
        var account = await SeedAsync();
        await using (var db = database.Create(account.UserId, account.TenantId))
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"LockoutEnd\" = now() + interval '15 minutes', \"AccessFailedCount\" = 5 WHERE \"UserId\" = {account.UserId}");
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        using var response = await ResetAsync(client, account, token, NewPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(account.UserId, (await LoginAsync(client, account, NewPassword)).Session.UserId);
    }

    [Fact]
    public async Task Refresh_racing_reset_cannot_leave_a_usable_old_session()
    {
        var account = await SeedAsync();
        var queue = new ResetQueue(); var email = new ResetInbox();
        await using var factory = CreateFactory(queue, email);
        using var client = factory.CreateClient();
        var session = await LoginAsync(client, account, OldPassword);
        var token = await RequestTokenAsync(factory, client, account, queue, email);
        var resetTask = ResetAsync(client, account, token, NewPassword);
        var refreshTask = client.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken });
        await Task.WhenAll(resetTask, refreshTask);
        using var reset = await resetTask;
        using var refresh = await refreshTask;
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains(refresh.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (refresh.StatusCode == HttpStatusCode.OK)
        {
            var rotated = (await refresh.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", rotated.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
            using var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { rotated.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        }
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.False(await db.AuthenticationSessions.AnyAsync(s => s.RevokedAt == null));
    }

    [Fact]
    public async Task Real_Hangfire_PostgreSQL_queue_delivers_through_Application_service()
    {
        var account = await SeedAsync();
        var email = new ResetInbox();
        await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?>
        {
            ["ResetEmail:Enabled"] = "true", ["ResetEmail:Host"] = "127.0.0.1",
            ["ResetEmail:FromAddress"] = "no-reply@example.test", ["ResetEmail:FrontendOrigin"] = "https://bizflow.example.test"
        }, configureServices: services =>
        {
            services.RemoveAll<IPasswordResetEmailSender>(); services.AddSingleton<IPasswordResetEmailSender>(email);
        });
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { identifier = "EMP001", tenantKey = account.Key });
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, factory.LastServerError?.ToString());
        var delivered = await email.First.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal("employee@example.test", delivered.Address);
        using var reset = await ResetAsync(client, account, delivered.Token, NewPassword);
        Assert.True(reset.StatusCode == HttpStatusCode.OK, factory.LastServerError?.ToString());
    }

    private AuthenticationFactory CreateFactory(ResetQueue queue, ResetInbox email) => new(database, configureServices: services =>
    {
        services.RemoveAll<IPasswordResetQueue>(); services.AddSingleton<IPasswordResetQueue>(queue);
        services.RemoveAll<IPasswordResetEmailSender>(); services.AddSingleton<IPasswordResetEmailSender>(email);
    });
    private static async Task<string> RequestTokenAsync(AuthenticationFactory factory, HttpClient client, Account account, ResetQueue queue, ResetInbox email)
    {
        using var requested = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { identifier = "EMP001", tenantKey = account.Key });
        Assert.True(requested.StatusCode == HttpStatusCode.Accepted, factory.LastServerError?.ToString());
        using var scope = factory.Services.CreateScope();
        var item = queue.Items.Single();
        await scope.ServiceProvider.GetRequiredService<PasswordResetDeliveryService>().DeliverAsync(item.Identifier, item.Key, item.Time, default);
        return Assert.Single(email.Messages).Token;
    }
    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, Account account, string token, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/reset-password", new { identifier = "EMP001", tenantKey = account.Key, resetToken = token, newPassword = password });
    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, Account account, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }
    private async Task<Account> SeedAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, OldPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        return new(user.Id, tenant.Id, tenant.TenantKey, user.SecurityStamp);
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key, string InitialStamp);
}

public sealed class ResetQueue : IPasswordResetQueue
{
    public ConcurrentQueue<(string Identifier, string? Key, DateTimeOffset Time)> Items { get; } = new();
    public void Enqueue(string normalizedIdentifier, string? tenantKey, DateTimeOffset requestedAt) => Items.Enqueue((normalizedIdentifier, tenantKey, requestedAt));
}
public sealed class ResetInbox : IPasswordResetEmailSender
{
    private readonly Channel<(string Address, string Token)> channel = Channel.CreateUnbounded<(string Address, string Token)>();
    public ConcurrentQueue<(string Address, string Token)> Messages { get; } = new();
    public TaskCompletionSource<(string Address, string Token)> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task SendAsync(string address, string token, CancellationToken cancellationToken)
    { Messages.Enqueue((address, token)); First.TrySetResult((address, token)); channel.Writer.TryWrite((address, token)); return Task.CompletedTask; }
    public async Task<(string Address, string Token)> NextAsync() => await channel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
}
