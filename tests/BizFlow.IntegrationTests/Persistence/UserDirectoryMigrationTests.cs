using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class UserDirectoryMigrationTests
{
    [Fact]
    public async Task Reference_migration_reapplies_but_does_not_silently_remove_custom_role_configuration()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001012648_ManagementScopeFoundation");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Code == "users.read"));
                await db.Database.MigrateAsync();
                var permissionId = await db.Permissions.IgnoreQueryFilters().Where(p => p.Code == "users.read").Select(p => p.Id).SingleAsync();
                Assert.Equal(3, await db.RolePermissions.IgnoreQueryFilters().CountAsync(rp => rp.PermissionId == permissionId));
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var role = Role.CreateCustom(seed.TenantId, "Directory readers", DateTimeOffset.UtcNow);
                db.Roles.Add(role); await db.SaveChangesAsync();
                var permissionId = await db.Permissions.Where(p => p.Code == "users.read").Select(p => p.Id).SingleAsync();
                db.RolePermissions.Add(new(role.Id, permissionId)); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261001012648_ManagementScopeFoundation"));
            }
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                Assert.Contains("20261001013618_UserDirectoryRead", await db.Database.GetAppliedMigrationsAsync());
                var permissionId = await db.Permissions.Where(p => p.Code == "users.read").Select(p => p.Id).SingleAsync();
                Assert.Equal(4, await db.RolePermissions.CountAsync(rp => rp.PermissionId == permissionId));
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
