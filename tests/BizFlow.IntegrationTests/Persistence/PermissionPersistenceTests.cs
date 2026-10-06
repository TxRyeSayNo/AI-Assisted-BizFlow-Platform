using BizFlow.Application.Common;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class PermissionPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Custom_role_cannot_be_assigned_across_tenants_in_EF_or_direct_SQL()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var role = Role.CreateCustom(a.TenantId, "Support", DateTimeOffset.UtcNow);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole(b.UserId, role.Id));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"UserRole\" (\"UserId\", \"RoleId\") VALUES ({b.UserId}, {role.Id})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Platform_permission_cannot_be_granted_to_custom_tenant_role()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var role = Role.CreateCustom(a.TenantId, "Administrator", DateTimeOffset.UtcNow);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionId = await SeedPermissionAsync(db, "PLATFORM");
        db.RolePermissions.Add(new RolePermission(role.Id, permissionId));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"RolePermission\" (\"RoleId\", \"PermissionId\") VALUES ({role.Id}, {permissionId})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Resolver_uses_persisted_grants_and_live_company_status_without_role_name_authority()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var role = Role.CreateCustom(a.TenantId, "Platform Administrator", DateTimeOffset.UtcNow);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole(a.UserId, role.Id));
        await db.SaveChangesAsync();
        var resolver = new AccessSnapshotProvider(db, TimeProvider.System);
        var pending = await resolver.ResolveAsync(a.UserId, a.TenantId, default);
        Assert.NotNull(pending);
        Assert.True(pending.IsUserActive);
        Assert.False(pending.IsTenantActive);
        Assert.Empty(pending.Grants);
        Assert.Null(await resolver.ResolveAsync(a.UserId, Guid.NewGuid(), default));

        var permissionId = await SeedPermissionAsync(db, "SELF");
        db.RolePermissions.Add(new RolePermission(role.Id, permissionId));
        await db.SaveChangesAsync();
        // Test-only lifecycle changes: provisioning/lifecycle services are a separate slice.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status" = 'ACTIVE'
            WHERE "CompanyId" = (SELECT "CompanyId" FROM "Tenant" WHERE "TenantId" = {a.TenantId})
            """);
        var active = await resolver.ResolveAsync(a.UserId, a.TenantId, default);
        Assert.NotNull(active);
        Assert.True(active.IsTenantActive);
        Assert.Equal(PermissionScope.Self, Assert.Single(active.Grants).Scope);
        Assert.Empty(active.ManagedDepartmentIds);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Role\" SET \"Status\" = 'INACTIVE' WHERE \"RoleId\" = {role.Id}");
        var revoked = await resolver.ResolveAsync(a.UserId, a.TenantId, default);
        Assert.NotNull(revoked);
        Assert.Empty(revoked.Grants);
    }

    private static async Task<Guid> SeedPermissionAsync(BizFlow.Infrastructure.Persistence.BizFlowDbContext db, string scope)
    {
        var id = Guid.CreateVersion7();
        var code = "test." + id.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
            VALUES ({id}, {code}, 'test', 'read', CAST({scope} AS permission_scope))
            """);
        return id;
    }
}
