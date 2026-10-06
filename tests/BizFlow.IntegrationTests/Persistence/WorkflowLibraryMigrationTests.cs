using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class WorkflowLibraryMigrationTests
{
    [Fact]
    public async Task Catalog_reapplies_and_rollback_preserves_custom_workflow_permissions()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001090500_WorkflowPersistenceFoundation");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Code == "workflows.read"));
                await db.Database.MigrateAsync();
                var ids = await db.Permissions.IgnoreQueryFilters().Where(p => p.Module == "workflows").Select(p => p.Id).ToArrayAsync();
                Assert.Equal(2, ids.Length); Assert.Equal(4, await db.RolePermissions.IgnoreQueryFilters().CountAsync(rp => ids.Contains(rp.PermissionId)));
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var role = Role.CreateCustom(seed.TenantId, "Workflow readers", DateTimeOffset.UtcNow);
                db.Roles.Add(role); await db.SaveChangesAsync();
                var permission = await db.Permissions.SingleAsync(p => p.Code == "workflows.read");
                db.RolePermissions.Add(new(role.Id, permission.Id)); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261001090500_WorkflowPersistenceFoundation"));
                Assert.Contains("20261001092159_WorkflowLibraryPermissions", await db.Database.GetAppliedMigrationsAsync());
                Assert.True(await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id));
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
