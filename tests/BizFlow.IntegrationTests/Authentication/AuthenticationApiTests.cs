using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace BizFlow.IntegrationTests.Authentication;

public sealed class AuthenticationFactory(PostgresFixture database, IReadOnlyDictionary<string, string?>? overrides = null,
    string? contentRoot = null, Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    public Exception? LastServerError { get; private set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (contentRoot is not null) builder.UseContentRoot(contentRoot);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "bizflow-tests", ["Jwt:Audience"] = "bizflow-tests",
                ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["ConnectionStrings:BizFlow"] = database.ConnectionString
            });
            if (overrides is not null) configuration.AddInMemoryCollection(overrides);
        });
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
            services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler>(new ExceptionObserver(error => LastServerError = error)));
            configureServices?.Invoke(services);
        });
    }
    private sealed class ExceptionObserver(Action<Exception> record) : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        { record(exception); return ValueTask.FromResult(false); }
    }
}

[Collection("Postgres")]
public sealed class AuthenticationApiTests(PostgresFixture database)
{
    private const string TestPassword = "Test-only!53-Password";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Employee_code_and_email_login_return_correct_tenant_and_live_bearer_session(bool useEmail)
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        { identifier = useEmail ? account.Email.ToUpperInvariant() : account.Code.ToLowerInvariant(), password = TestPassword });
        Assert.True(login.StatusCode == HttpStatusCode.OK, factory.LastServerError?.ToString());
        Assert.True(login.Headers.CacheControl?.NoStore);
        var result = (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        Assert.Equal(account.TenantId, result.Session.TenantId);
        Assert.Equal(account.UserId, result.Session.UserId);
        Assert.Contains("test.read", result.Session.Permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var identity = await client.GetFromJsonAsync<JsonElement>("/_auth-probe/identity");
        Assert.Equal(account.TenantId, identity.GetProperty("tenantId").GetGuid());
        await using var db = database.Create(account.UserId, account.TenantId);
        var session = await db.AuthenticationSessions.SingleAsync();
        Assert.NotEqual(result.RefreshToken, session.TokenHash);
        Assert.Equal(64, session.TokenHash.Length);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "AUTH.LOGIN_SUCCEEDED"));
    }

    [Fact]
    public async Task Ambiguous_identifier_requires_workspace_then_resolves_only_that_tenant()
    {
        var code = "shared-" + Guid.NewGuid().ToString("N");
        var a = await SeedAsync(code);
        var b = await SeedAsync(code);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var ambiguous = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = code, password = TestPassword });
        Assert.Equal(HttpStatusCode.Conflict, ambiguous.StatusCode);
        Assert.Equal("AUTH.TENANT_CONTEXT_REQUIRED", (await ambiguous.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var result = await LoginAsync(client, b);
        Assert.Equal(b.TenantId, result.Session.TenantId);
        Assert.NotEqual(a.TenantId, result.Session.TenantId);
    }

    [Fact]
    public async Task Invalid_and_locked_credentials_are_generic_and_failures_persist()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = account.Code, password = "wrong-password", tenantKey = account.Key });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
            Assert.DoesNotContain("wrong-password", await failure.Content.ReadAsStringAsync());
        }
        using var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = account.Code, password = TestPassword, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        var user = await db.Users.SingleAsync();
        Assert.Equal(5, user.AccessFailedCount);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.Empty(await db.AuthenticationSessions.ToListAsync());
        Assert.Equal(6, await db.AuditLogs.CountAsync(a => a.Action == "AUTH.LOGIN_FAILED"));
    }

    [Fact]
    public async Task Refresh_rotates_and_replay_revokes_successor_and_access_token()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var original = await LoginAsync(client, account);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", rotated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        using var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        using var successor = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, successor.StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.All(await db.AuthenticationSessions.ToListAsync(), s => Assert.NotNull(s.RevokedAt));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "AUTH.REPLAY_DETECTED"));
    }

    [Fact]
    public async Task Simultaneous_refresh_has_one_response_winner_and_replay_closes_family()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, account);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/v1/auth/refresh", new { login.RefreshToken })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        foreach (var response in responses) response.Dispose();
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(2, await db.AuthenticationSessions.CountAsync());
        Assert.All(await db.AuthenticationSessions.ToListAsync(), s => Assert.NotNull(s.RevokedAt));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("stamp")]
    [InlineData("must-change")]
    public async Task Current_account_tenant_company_and_security_stamp_invalidate_access(string change)
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        switch (change)
        {
            case "user": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {account.UserId}"); break;
            case "tenant": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\" = 'SUSPENDED' WHERE \"TenantId\" = {account.TenantId}"); break;
            case "company": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Company\" SET \"Status\" = 'SUSPENDED' WHERE \"CompanyId\" = (SELECT \"CompanyId\" FROM \"Tenant\" WHERE \"TenantId\" = {account.TenantId})"); break;
            case "stamp": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"SecurityStamp\" = {Guid.NewGuid().ToString("N")} WHERE \"UserId\" = {account.UserId}"); break;
            case "must-change": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"MustChangePassword\" = TRUE WHERE \"UserId\" = {account.UserId}"); break;
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        if (change != "stamp")
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { login.RefreshToken })).StatusCode);
        // Password reset must also revoke refresh families; that orchestration is a separate test gate.
    }

    [Fact]
    public async Task Authenticated_probe_enforces_application_tenant_scope_and_audits_denial()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, a);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/_auth-probe/scope/{a.TenantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/_auth-probe/scope/{b.TenantId}")).StatusCode);
        await using var db = database.Create(a.UserId, a.TenantId);
        Assert.True(await db.AuditLogs.AnyAsync(audit => audit.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Platform_identity_is_tenantless_and_does_not_implicitly_authorize_tenant_access()
    {
        var tenantAccount = await SeedAsync();
        await using var db = database.Create(database.PlatformUserId, null);
        var platform = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(platform, TestPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\" = {hash} WHERE \"UserId\" = {platform.Id}");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var response = (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        Assert.Null(response.Session.TenantId);
        Assert.Empty(response.Session.Permissions);
        client.DefaultRequestHeaders.Authorization = new("Bearer", response.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/_auth-probe/scope/{tenantAccount.TenantId}")).StatusCode);
    }

    [Fact]
    public async Task Expired_refresh_and_forged_access_tokens_are_rejected()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, account);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-signed-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/_auth-probe/identity")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AuthenticationSession" SET "CreatedAt" = {DateTimeOffset.UtcNow.AddDays(-2)},
                "ExpiresAt" = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE "UserId" = {account.UserId}
            """);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { login.RefreshToken })).StatusCode);
        Assert.All(await db.AuthenticationSessions.ToListAsync(), session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task Auth_input_validation_rejects_missing_fields_and_does_not_echo_credentials()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "", password = "secret-not-for-response" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.DoesNotContain("secret-not-for-response", await response.Content.ReadAsStringAsync());
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "short-secret" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refresh.StatusCode);
        Assert.DoesNotContain("short-secret", await refresh.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Auth_rate_limit_uses_standard_429_envelope()
    {
        await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?> { ["Authentication:RateLimits:PerIpPerMinute"] = "1" });
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "missing", password = "invalid" });
        using var denied = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "missing", password = "invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.Equal("RATE_LIMIT.EXCEEDED", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = account.Code, password = TestPassword, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }

    private async Task<Account> SeedAsync(string? code = null)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        code ??= "AUTH-" + Guid.NewGuid().ToString("N");
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, TestPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "EmployeeCode" = {code}, "NormalizedEmployeeCode" = {code.ToUpperInvariant()},
                "Email" = {email}, "NormalizedEmail" = {email.ToUpperInvariant()}, "PasswordHash" = {hash}
            WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        var role = Role.CreateCustom(tenant.Id, "Test tenant access", DateTimeOffset.UtcNow);
        db.Roles.Add(role); await db.SaveChangesAsync();
        // Permission is migration-owned reference data in production; seed isolated test catalog once.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
            VALUES ({Guid.CreateVersion7()}, 'test.read', 'test', 'read', 'TENANT') ON CONFLICT ("Code") DO NOTHING
            """);
        var permission = await db.Permissions.SingleAsync(p => p.Code == "test.read");
        db.RolePermissions.Add(new(role.Id, permission.Id)); db.UserRoles.Add(new(user.Id, role.Id));
        await db.SaveChangesAsync();
        return new(user.Id, tenant.Id, tenant.TenantKey, code, email);
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key, string Code, string Email);
}

// Test-only authenticated probes, never mapped in the production app. They prove middleware and
// Application authorization wiring, not completion of the forthcoming business API isolation suite.
[ApiController, Authorize, Route("_auth-probe")]
public sealed class AuthenticationProbeController(ITenantContext tenant, IResourceAuthorizer authorizer) : ControllerBase
{
    [HttpGet("identity")]
    public object Identity() => new { tenant.UserId, tenant.TenantId };
    [HttpGet("scope/{tenantId:guid}")]
    public async Task<IActionResult> Scope(Guid tenantId)
    {
        await authorizer.AuthorizeAsync("test.read", new ResourceScope(tenantId));
        return Ok();
    }
}
