using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class AuditReadMigrationTests
{
    [Fact]
    public async Task Permission_migration_reapplies_but_preserves_custom_role_configuration()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001092159_WorkflowLibraryPermissions");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Code == "audit.read" || p.Code == "platform.audit.read"));
                await db.Database.MigrateAsync();
                var codes = new[] { "audit.read", "platform.audit.read" };
                var ids = await db.Permissions.IgnoreQueryFilters().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToArrayAsync();
                Assert.Equal(2, ids.Length); Assert.Equal(2, await db.RolePermissions.IgnoreQueryFilters().CountAsync(rp => ids.Contains(rp.PermissionId)));
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var role = Role.CreateCustom(seed.TenantId, "Audit reviewers", DateTimeOffset.UtcNow);
                db.Roles.Add(role); await db.SaveChangesAsync();
                var permission = await db.Permissions.SingleAsync(p => p.Code == "audit.read");
                db.RolePermissions.Add(new(role.Id, permission.Id)); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261001092159_WorkflowLibraryPermissions"));
                Assert.Contains("20261001094232_AuditReadPermissions", await db.Database.GetAppliedMigrationsAsync());
                Assert.True(await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id));
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
