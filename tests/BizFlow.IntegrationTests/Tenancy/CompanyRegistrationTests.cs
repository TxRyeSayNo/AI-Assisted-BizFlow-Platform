using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Tenancy;

[Collection("Postgres")]
public sealed class CompanyRegistrationTests(PostgresFixture database)
{
    private const string Password = "Platform-test-password!183";
    private const string Endpoint = "/api/v1/platform/company-registrations";

    [Fact]
    public async Task Platform_registry_requires_a_current_explicit_grant_and_audits_denial()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        var identity = await SeedPlatformAsync(false);
        var login = await LoginAsync(client, identity.Code);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        await using var db = database.Create(identity.Id, null);
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.ActorId == identity.Id && a.Action == "SECURITY.ACCESS_DENIED"));
        await GrantAsync(identity.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\" = {identity.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task Tenant_administrator_role_name_does_not_grant_platform_registry_access()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        var role = BizFlow.Domain.Security.Role.CreateCustom(tenant.Id, "PLATFORM_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, "EMP001", tenant.TenantKey);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var response = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("admin@example.test", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registry_filters_paginates_deterministically_and_never_changes_company_status()
    {
        var prefix = Guid.NewGuid().ToString("N");
        await using var db = database.Create(database.PlatformUserId, null);
        var time = DateTimeOffset.UtcNow;
        var first = Company.Register(prefix + "-A", "Queue % literal", "queue-a@example.test", time);
        var second = Company.Register(prefix + "-B", "Queue other", "queue-b@example.test", time);
        db.Companies.AddRange(first, second); await db.SaveChangesAsync();
        var identity = await SeedPlatformAsync(true);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, identity.Code);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var response = await client.GetAsync(Endpoint + $"?search={prefix}&status=PENDING&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<CompanyRegistrationPage>())!;
        Assert.Equal(2, page.Total); Assert.Equal(1, page.PageSize);
        var next = (await client.GetFromJsonAsync<CompanyRegistrationPage>(Endpoint + $"?search={prefix}&status=PENDING&pageSize=1&page=2"))!;
        Assert.NotEqual(Assert.Single(page.Items).CompanyId, Assert.Single(next.Items).CompanyId);
        var repeated = (await client.GetFromJsonAsync<CompanyRegistrationPage>(Endpoint + $"?search={prefix}&status=PENDING&pageSize=1"))!;
        Assert.Equal(page.Items[0].CompanyId, repeated.Items[0].CompanyId);
        var literal = (await client.GetFromJsonAsync<CompanyRegistrationPage>(Endpoint + "?search=%25"))!;
        Assert.Contains(literal.Items, c => c.CompanyId == first.Id);
        Assert.DoesNotContain(literal.Items, c => c.CompanyId == second.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + "?status=APPROVED")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + "?pageSize=101")).StatusCode);
        Assert.All(await db.Companies.IgnoreQueryFilters().Where(c => c.Id == first.Id || c.Id == second.Id).ToListAsync(), c => Assert.Equal(CompanyStatus.Pending, c.Status));
    }

    private async Task<(Guid Id, string Code)> SeedPlatformAsync(bool grant)
    {
        var code = "PLAT-" + Guid.NewGuid().ToString("N");
        var user = UserAccount.CreatePlatformAdministrator(code, code + "@example.test", "Queue administrator", "placeholder", DateTimeOffset.UtcNow);
        user.ResetPassword(new PasswordHasher<UserAccount>().HashPassword(user, Password), DateTimeOffset.UtcNow);
        await using var db = database.Create(database.PlatformUserId, null);
        db.Users.Add(user); await db.SaveChangesAsync();
        if (grant) await GrantAsync(user.Id);
        return (user.Id, code);
    }
    private async Task GrantAsync(Guid userId)
    {
        await using var db = database.Create(database.PlatformUserId, null);
        // Test-only assignment. Production provisioning/bootstrap is a separate required slice.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRole" ("UserId", "RoleId") SELECT {userId}, "RoleId" FROM "Role"
            WHERE "Name" = 'PLATFORM_ADMIN' AND "IsSystem";
            """);
    }
    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, string identifier, string? tenantKey = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password = Password, tenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }
}
