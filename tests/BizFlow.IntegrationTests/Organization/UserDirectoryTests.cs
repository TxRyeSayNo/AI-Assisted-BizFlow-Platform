using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Organization;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Organization;

[Collection("Postgres")]
public sealed class UserDirectoryTests(PostgresFixture database)
{
    private const string Password = "Directory-test-only!7293";
    private const string Endpoint = "/api/v1/users";

    [Fact]
    public async Task Directory_requires_explicit_live_permission_and_never_role_name_authority()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var fakeRole = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(fakeRole); await db.SaveChangesAsync();
        db.UserRoles.Add(new(account.UserId, fakeRole.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        var permission = await db.Permissions.SingleAsync(p => p.Code == "users.read");
        var grant = new RolePermission(fakeRole.Id, permission.Id);
        db.RolePermissions.Add(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        db.RolePermissions.Remove(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "SECURITY.ACCESS_DENIED" && a.ActorId == account.UserId));
    }

    [Theory]
    [InlineData("COMPANY_ADMIN")]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    public async Task Approved_system_roles_have_explicit_tenant_directory_read_not_user_mutation(string roleName)
    {
        var account = await SeedAsync(roleName);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = await db.Roles.SingleAsync(r => r.Name == roleName && r.IsSystem);
        var grants = await db.RolePermissions.Where(rp => rp.RoleId == role.Id)
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code).ToListAsync();
        Assert.Contains("users.read", grants);
        Assert.DoesNotContain("users.update", grants); Assert.DoesNotContain("users.create", grants);
    }

    [Fact]
    public async Task Tenant_and_soft_delete_filters_cover_rows_department_names_counts_and_search_and_never_return_credentials()
    {
        var account = await SeedAsync("EMPLOYEE"); var foreign = await SeedAsync("EMPLOYEE");
        await using var db = database.Create(account.UserId, account.TenantId);
        var department = Department.Create(account.TenantId, "OPS", "Workspace operations", null, DateTimeOffset.UtcNow);
        db.Departments.Add(department); await db.SaveChangesAsync();
        var colleague = UserAccount.CreateTenantUser(account.TenantId, "COWORKER", "coworker@example.test", "Visible Coworker", "sensitive-hash-sentinel", DateTimeOffset.UtcNow, department.Id);
        var deleted = UserAccount.CreateTenantUser(account.TenantId, "DELETED", "deleted@example.test", "Deleted Sentinel", "hash", DateTimeOffset.UtcNow);
        db.Users.AddRange(colleague, deleted); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DeletedAt\" = now() WHERE \"UserId\" = {deleted.Id}");
        await using var other = database.Create(foreign.UserId, foreign.TenantId);
        await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"FullName\" = 'Foreign confidential sentinel' WHERE \"UserId\" = {foreign.UserId}");
        var foreignDepartment = Department.Create(foreign.TenantId, "SECRET", "Foreign department", null, DateTimeOffset.UtcNow);
        other.Departments.Add(foreignDepartment); await other.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.TenantId.ToString());
        using var response = await client.GetAsync(Endpoint + $"?tenantId={foreign.TenantId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<UserDirectoryPage>())!;
        Assert.Equal(2, page.Total); Assert.DoesNotContain(page.Items, u => u.UserId == foreign.UserId || u.UserId == deleted.Id);
        Assert.Equal("Workspace operations", Assert.Single(page.Items, u => u.UserId == colleague.Id).DepartmentName);
        Assert.Null(Assert.Single(page.Items, u => u.UserId == account.UserId).DepartmentName);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var row in json.RootElement.GetProperty("items").EnumerateArray())
            Assert.Equal(new[] { "departmentId", "departmentName", "email", "employeeCode", "fullName", "status", "userId" }, row.EnumerateObject().Select(p => p.Name).Order().ToArray());
        foreach (var query in new[] { "?search=Foreign", "?search=Deleted", $"?departmentId={foreignDepartment.Id}" })
        {
            var empty = (await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + query))!;
            Assert.Equal(0, empty.Total); Assert.Empty(empty.Items);
        }
        Assert.Equal(colleague.Id, Assert.Single((await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + $"?departmentId={department.Id}"))!.Items).UserId);
    }

    [Fact]
    public async Task Pagination_literal_search_and_status_filters_obey_the_user_state_catalog()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.UserId, account.TenantId);
        var now = DateTimeOffset.UtcNow;
        var inactive = UserAccount.CreateTenantUser(account.TenantId, "INACTIVE", "inactive@example.test", "Literal % name", "hash", now);
        var locked = UserAccount.CreateTenantUser(account.TenantId, "LOCKED", "locked@example.test", "Locked user", "hash", now.AddSeconds(1));
        db.Users.AddRange(inactive, locked); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {inactive.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'LOCKED' WHERE \"UserId\" = {locked.Id}");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var first = (await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + "?pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + "?pageSize=1&page=2"))!;
        Assert.Equal(3, first.Total); Assert.Equal(account.UserId, Assert.Single(first.Items).UserId);
        Assert.Equal(inactive.Id, Assert.Single(second.Items).UserId);
        foreach (var query in new[] { "?status=INACTIVE", "?search=%25", "?search=inactive%40example.test" })
            Assert.Equal(inactive.Id, Assert.Single((await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + query))!.Items).UserId);
        Assert.Equal(locked.Id, Assert.Single((await client.GetFromJsonAsync<UserDirectoryPage>(Endpoint + "?status=LOCKED"))!.Items).UserId);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100", "?status=PENDING", "?departmentId=00000000-0000-0000-0000-000000000000", "?search=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
    }

    [Fact]
    public async Task Tenantless_platform_and_later_inactive_tenant_cannot_read_the_directory()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await using var platformDb = database.Create(database.PlatformUserId, null);
        var platform = await platformDb.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(platform, Password);
        await platformDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\" = {hash} WHERE \"UserId\" = {platform.Id}");
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        var account = await SeedAsync("EMPLOYEE"); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\" = 'SUSPENDED' WHERE \"TenantId\" = {account.TenantId}");
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
        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync();
        }
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
