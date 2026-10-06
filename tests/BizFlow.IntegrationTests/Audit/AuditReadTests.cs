using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Audit;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Audit;

[Collection("Postgres")]
public sealed class AuditReadTests(PostgresFixture database)
{
    private const string Password = "Audit-read-test-only!7254";
    private const string Endpoint = "/api/v1/audit-logs";

    [Fact]
    public async Task Tenant_audit_read_uses_live_grants_and_cannot_be_obtained_from_a_role_name()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        var permission = await db.Permissions.SingleAsync(p => p.Code == "audit.read");
        var grant = new RolePermission(role.Id, permission.Id); db.RolePermissions.Add(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        db.RolePermissions.Remove(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Theory]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    public async Task Nonadministrator_system_roles_do_not_receive_tenant_wide_audit_access(string role)
    {
        var account = await SeedAsync(role);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task Tenant_filters_cover_counts_payloads_actor_object_filters_and_forged_scope_inputs()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        var target = Guid.NewGuid();
        await using var db = database.Create(a.UserId, a.TenantId);
        var own = AuditLog.RoleConfiguration(a.TenantId, a.UserId, target, "Own role", [], [Guid.NewGuid()], DateTimeOffset.UtcNow);
        db.AuditLogs.Add(own); await db.SaveChangesAsync();
        await using var other = database.Create(b.UserId, b.TenantId);
        var foreign = AuditLog.RoleConfiguration(b.TenantId, b.UserId, Guid.NewGuid(), "Foreign secret sentinel", [], [], DateTimeOffset.UtcNow);
        other.AuditLogs.Add(foreign); await other.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        using var response = await client.GetAsync(Endpoint + $"?action=ROLE.PERMISSIONS_CONFIGURED&tenantId={b.TenantId}&scope=platform");
        Assert.True(response.Headers.CacheControl?.NoStore); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<AuditPage>())!;
        Assert.Equal(1, page.Total); var row = Assert.Single(page.Items); Assert.Equal(own.Id, row.AuditLogId);
        Assert.Equal("Own role", row.After!.Value.GetProperty("name").GetString()); Assert.NotNull(row.Before);
        Assert.DoesNotContain("Foreign secret", await response.Content.ReadAsStringAsync());
        var filtered = (await client.GetFromJsonAsync<AuditPage>(Endpoint + $"?actorId={a.UserId}&actorType=USER&objectType=Role&objectId={target}"))!;
        Assert.Equal(own.Id, Assert.Single(filtered.Items).AuditLogId);
        foreach (var query in new[] { $"?actorId={b.UserId}", $"?objectId={foreign.ObjectId}", "?action=%25", "?objectType=Role%25" })
            Assert.Empty((await client.GetFromJsonAsync<AuditPage>(Endpoint + query))!.Items);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "action", "actorId", "actorType", "after", "auditLogId", "before", "createdAt", "metadata", "objectId", "objectType" },
            json.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task Stable_paging_and_explicit_UTC_half_open_date_bounds_are_enforced()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.UserId, account.TenantId);
        var time = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var first = AuditLog.RoleConfiguration(account.TenantId, account.UserId, Guid.NewGuid(), "A", null, [], time);
        var second = AuditLog.RoleConfiguration(account.TenantId, account.UserId, Guid.NewGuid(), "B", null, [], time.AddDays(1));
        db.AuditLogs.AddRange(first, second); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var filter = Endpoint + "?action=ROLE.CREATED&pageSize=1";
        var page = (await client.GetFromJsonAsync<AuditPage>(filter))!; Assert.Equal(2, page.Total); Assert.Equal(second.Id, Assert.Single(page.Items).AuditLogId);
        Assert.Equal(first.Id, Assert.Single((await client.GetFromJsonAsync<AuditPage>(filter + "&page=2"))!.Items).AuditLogId);
        var bounded = (await client.GetFromJsonAsync<AuditPage>(Endpoint + "?action=ROLE.CREATED&from=2026-09-01T07:00:00%2B07:00&until=2026-09-02T00:00:00Z"))!;
        Assert.Equal(first.Id, Assert.Single(bounded.Items).AuditLogId);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100", "?actorType=ADMIN", "?actorId=00000000-0000-0000-0000-000000000000",
            "?objectId=invalid", "?from=2026-09-01", "?from=2026-09-02T00:00:00Z&until=2026-09-01T00:00:00Z", "?action=" + new string('x', 101), "?objectType=" + new string('x', 61) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(Endpoint)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "ROLE.CREATED"));
    }

    [Fact]
    public async Task Platform_scope_reads_only_platform_events_with_an_explicit_live_platform_grant()
    {
        var tenant = await SeedAsync("COMPANY_ADMIN");
        await using var tenantDb = database.Create(tenant.UserId, tenant.TenantId);
        var tenantEvent = AuditLog.RoleConfiguration(tenant.TenantId, tenant.UserId, Guid.NewGuid(), "Tenant hidden", null, [], DateTimeOffset.UtcNow);
        tenantDb.AuditLogs.Add(tenantEvent); await tenantDb.SaveChangesAsync();
        await using var db = database.Create(database.PlatformUserId, null);
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\" = {hash} WHERE \"UserId\" = {user.Id}");
        // Isolate this test from other disposable-fixture tests assigning the same platform actor.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\" = {user.Id}");
        var platformEvent = AuditLog.DeniedAccess(null, user.Id, "platform.company-registrations.read", "PermissionDenied", DateTimeOffset.UtcNow);
        db.AuditLogs.Add(platformEvent); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "UserRole" ("UserId", "RoleId") SELECT {user.Id}, "RoleId" FROM "Role" WHERE "IsSystem" AND "Name" = 'PLATFORM_ADMIN'""");
        var page = (await client.GetFromJsonAsync<AuditPage>(Endpoint + $"?tenantId={tenant.TenantId}&pageSize=100"))!;
        Assert.Contains(page.Items, a => a.AuditLogId == platformEvent.Id); Assert.DoesNotContain(page.Items, a => a.AuditLogId == tenantEvent.Id);
        Assert.Empty((await client.GetFromJsonAsync<AuditPage>(Endpoint + $"?objectId={tenantEvent.ObjectId}"))!.Items);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\" = {user.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        using var tenantClient = factory.CreateClient(); await LoginAsync(tenantClient, tenant);
        Assert.DoesNotContain((await tenantClient.GetFromJsonAsync<AuditPage>(Endpoint + "?pageSize=100"))!.Items, a => a.AuditLogId == platformEvent.Id);
    }

    [Fact]
    public async Task Suspended_tenant_cannot_read_historical_audit_events()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\"='SUSPENDED' WHERE \"TenantId\"={account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    private async Task<Account> SeedAsync(string? roleName = null)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        if (roleName is not null) { var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName); db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync(); }
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
