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
public sealed class RoleManagementTests(PostgresFixture database)
{
    private const string Password = "Role-test-password!278";

    [Fact]
    public async Task Role_names_do_not_grant_authority_and_live_grant_removal_is_enforced()
    {
        var account = await SeedAsync(false);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/roles")).StatusCode);
        await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/roles", new { name = "Denied" })).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        var pretend = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(pretend); db.UserRoles.Add(new(account.UserId, pretend.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/roles")).StatusCode);
        await GrantAsync(account);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/roles")).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UserRole\" WHERE \"UserId\" = {account.UserId}");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/roles")).StatusCode);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.ActorId == account.UserId && a.Action == "SECURITY.ACCESS_DENIED"));
        Assert.False(await db.Roles.AnyAsync(r => r.Name == "Denied"));
    }

    [Fact]
    public async Task Create_update_and_permission_removal_are_atomic_audited_and_live()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, account);
        var page = (await client.GetFromJsonAsync<RolePage>("/api/v1/roles"))!;
        var grant = Assert.Single(page.AvailablePermissions, p => p.Code == "roles.configure");
        Assert.DoesNotContain(page.AvailablePermissions, p => p.Scope == "PLATFORM");
        Assert.DoesNotContain(page.Items, r => r.Name == "PLATFORM_ADMIN" && r.IsSystem);
        Assert.Contains(page.Items, r => r.Name == "COMPANY_ADMIN" && r.IsSystem);
        var created = await CreateAsync(client, "Support");
        Assert.False(created.IsSystem); Assert.Empty(created.PermissionIds);
        using var update = await SetAsync(client, created, [grant.PermissionId]);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.True(update.Headers.CacheControl?.NoStore);
        var changed = (await update.Content.ReadFromJsonAsync<RoleRow>())!;
        Assert.NotEqual(created.ETag, changed.ETag);
        Assert.Equal(changed.ETag, update.Headers.ETag?.ToString());

        // Test-only assignment: production user assignment belongs to FR-ORG-001/002.
        await using var db = database.Create(account.UserId, account.TenantId);
        var member = UserAccount.CreateTenantUser(account.TenantId, "ROLE-MEMBER", "role-member@example.test", "Role member", "placeholder", DateTimeOffset.UtcNow);
        member.ResetPassword(new PasswordHasher<UserAccount>().HashPassword(member, Password), DateTimeOffset.UtcNow);
        db.Users.Add(member); db.UserRoles.Add(new(member.Id, created.RoleId)); await db.SaveChangesAsync();
        using var memberClient = factory.CreateClient();
        await LoginAsync(memberClient, account with { UserId = member.Id }, "ROLE-MEMBER");
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/v1/roles")).StatusCode);
        using var remove = await SetAsync(client, changed, []);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/v1/roles")).StatusCode);
        var audits = await db.AuditLogs.Where(a => a.ObjectId == created.RoleId).OrderBy(a => a.CreatedAt).ToListAsync();
        Assert.Equal(3, audits.Count);
        Assert.All(audits, a => { Assert.Equal(account.UserId, a.ActorId); Assert.Equal(account.TenantId, a.TenantId); });
        Assert.Equal("ROLE.CREATED", audits[0].Action);
        Assert.Contains(grant.PermissionId.ToString(), audits[1].AfterJson!);
        Assert.Contains(grant.PermissionId.ToString(), audits[2].BeforeJson!);
        Assert.DoesNotContain(grant.PermissionId.ToString(), audits[2].AfterJson!);
    }

    [Fact]
    public async Task Foreign_roles_platform_permissions_and_system_edits_are_rejected_without_mutation()
    {
        var a = await SeedAsync(); var b = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); using var other = factory.CreateClient();
        await LoginAsync(client, a); await LoginAsync(other, b);
        var foreign = await CreateAsync(other, "Foreign private role");
        var own = await CreateAsync(client, "Own role");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        var page = (await client.GetFromJsonAsync<RolePage>("/api/v1/roles?tenantId=" + b.TenantId))!;
        Assert.DoesNotContain(page.Items, r => r.RoleId == foreign.RoleId);
        using var cross = await SetAsync(client, foreign, []);
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        Assert.DoesNotContain("Foreign private role", await cross.Content.ReadAsStringAsync());
        var system = Assert.Single(page.Items, r => r.Name == "COMPANY_ADMIN" && r.IsSystem);
        using var protectedRole = await SetAsync(client, system, []);
        Assert.Equal(HttpStatusCode.Forbidden, protectedRole.StatusCode);
        using var elevated = await SetAsync(client, own, [Guid.Parse("019f7a64-0000-7000-8000-000000000001")]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, elevated.StatusCode);
        using var unknown = await SetAsync(client, own, [Guid.NewGuid()]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        await using var db = database.Create(a.UserId, a.TenantId);
        Assert.False(await db.RolePermissions.AnyAsync(p => p.RoleId == own.RoleId));
        Assert.False(await db.AuditLogs.AnyAsync(log => log.Action == "ROLE.PERMISSIONS_CONFIGURED"));
        var denials = await db.AuditLogs.Where(log => log.Action == "SECURITY.ACCESS_DENIED").ToListAsync();
        Assert.Equal(4, denials.Count);
        Assert.All(denials, log => Assert.Equal(a.UserId, log.ActorId));
        Assert.All(denials, log => Assert.DoesNotContain(foreign.RoleId.ToString(), log.MetadataJson!));
    }

    [Fact]
    public async Task Version_conflicts_prevent_lost_updates_including_concurrent_junction_changes()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, account);
        var role = await CreateAsync(client, "Concurrent");
        var grant = Assert.Single((await client.GetFromJsonAsync<RolePage>("/api/v1/roles"))!.AvailablePermissions, p => p.Code == "roles.configure");
        var results = await Task.WhenAll(SetAsync(client, role, [grant.PermissionId]), SetAsync(client, role, []));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("ROLE.VERSION_CONFLICT", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var noTag = await client.PutAsJsonAsync($"/api/v1/roles/{role.RoleId}/permissions", new { permissionIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noTag.StatusCode);
        foreach (var result in results) result.Dispose();
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ObjectId == role.RoleId && a.Action == "ROLE.PERMISSIONS_CONFIGURED"));
    }

    [Fact]
    public async Task Duplicate_names_and_invalid_sets_do_not_leave_partial_roles_or_audits()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var role = await CreateAsync(client, "Unique");
        using var duplicate = await client.PostAsJsonAsync("/api/v1/roles", new { name = " Unique " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var invalid = await SetAsync(client, role, [Guid.Empty]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync("/api/v1/roles?pageSize=101")).StatusCode);
        var page = (await client.GetFromJsonAsync<RolePage>("/api/v1/roles?search=Unique&pageSize=1"))!;
        Assert.Equal(1, page.Total); Assert.Equal(role.RoleId, Assert.Single(page.Items).RoleId);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "ROLE.CREATED"));
    }

    private async Task<Account> SeedAsync(bool grant = true)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        var account = new Account(user.Id, tenant.Id, tenant.TenantKey);
        if (grant) await GrantAsync(account);
        return account;
    }
    private async Task GrantAsync(Account account)
    {
        await using var db = database.Create(account.UserId, account.TenantId);
        var systemRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        db.UserRoles.Add(new(account.UserId, systemRole.Id)); await db.SaveChangesAsync();
    }
    private static async Task LoginAsync(HttpClient client, Account account, string code = "EMP001")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = code, password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private static async Task<RoleRow> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/roles", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<RoleRow>())!;
    }
    private static Task<HttpResponseMessage> SetAsync(HttpClient client, RoleRow role, Guid[] ids)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/roles/{role.RoleId}/permissions") { Content = JsonContent.Create(new { permissionIds = ids }) };
        request.Headers.TryAddWithoutValidation("If-Match", role.ETag);
        return client.SendAsync(request);
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key);
}
