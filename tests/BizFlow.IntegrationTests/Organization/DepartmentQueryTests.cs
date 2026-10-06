using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Organization;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Organization;

[Collection("Postgres")]
public sealed class DepartmentQueryTests(PostgresFixture database)
{
    private const string Password = "Department-reader-test!813";
    private const string Endpoint = "/api/v1/departments";

    [Fact]
    public async Task Department_catalog_rejects_anonymous_and_tenantless_platform_accounts()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await using var db = database.Create(database.PlatformUserId, null);
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\" = {hash} WHERE \"UserId\" = {user.Id}");
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.ActorId == user.Id && a.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Authenticated_employee_without_grants_sees_only_own_tenant_even_with_forged_tenant_inputs()
    {
        var a = await SeedAsync("Visible company");
        var b = await SeedAsync("Hidden tenant B");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var session = await LoginAsync(client, a);
        Assert.Empty(session.Session.Permissions);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        using var response = await client.GetAsync(Endpoint + $"?tenantId={b.TenantId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<DepartmentPage>())!;
        Assert.Equal(2, page.Total);
        Assert.Equal(new[] { a.ParentId, a.ChildId }, page.Items.Select(d => d.DepartmentId));
        Assert.Null(page.Items[0].ParentDepartmentId);
        Assert.Equal(a.ParentId, page.Items[1].ParentDepartmentId);
        Assert.DoesNotContain(page.Items, d => d.DepartmentId == b.ParentId || d.DepartmentId == b.ChildId);
        var hidden = (await client.GetFromJsonAsync<DepartmentPage>(Endpoint + "?search=Hidden%20tenant%20B"))!;
        Assert.Empty(hidden.Items); Assert.Equal(0, hidden.Total);
        await using var db = database.Create(a.UserId, a.TenantId);
        Assert.Equal(2, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task Catalog_filters_and_pages_without_inventing_business_states()
    {
        var account = await SeedAsync("Filter % literal");
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Department\" SET \"Status\" = 'INACTIVE' WHERE \"DepartmentId\" = {account.ChildId}");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, account);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var first = (await client.GetFromJsonAsync<DepartmentPage>(Endpoint + "?pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<DepartmentPage>(Endpoint + "?pageSize=1&page=2"))!;
        Assert.Equal(2, first.Total); Assert.Equal(1, first.PageSize);
        Assert.Equal(account.ParentId, Assert.Single(first.Items).DepartmentId);
        Assert.Equal(account.ChildId, Assert.Single(second.Items).DepartmentId);
        var active = (await client.GetFromJsonAsync<DepartmentPage>(Endpoint + "?status=ACTIVE"))!;
        Assert.Equal(account.ParentId, Assert.Single(active.Items).DepartmentId);
        var literal = (await client.GetFromJsonAsync<DepartmentPage>(Endpoint + "?search=%25"))!;
        Assert.Equal(account.ParentId, Assert.Single(literal.Items).DepartmentId);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?status=PENDING", "?page=2147483647&pageSize=100" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("tenant")]
    public async Task Current_inactive_account_or_tenant_cannot_use_an_earlier_access_token(string change)
    {
        var account = await SeedAsync("Inactive context");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, account);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        await using var db = database.Create(account.UserId, account.TenantId);
        if (change == "user")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {account.UserId}");
        else
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\" = 'SUSPENDED' WHERE \"TenantId\" = {account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    private async Task<Account> SeedAsync(string name)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        var now = DateTimeOffset.UtcNow;
        var parent = Department.Create(tenant.Id, "PARENT", name, null, now);
        var child = Department.Create(tenant.Id, "CHILD", "Child department", parent.Id, now.AddSeconds(1));
        db.Departments.AddRange(parent, child); await db.SaveChangesAsync();
        return new(user.Id, tenant.Id, tenant.TenantKey, parent.Id, child.Id);
    }
    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key, Guid ParentId, Guid ChildId);
}
