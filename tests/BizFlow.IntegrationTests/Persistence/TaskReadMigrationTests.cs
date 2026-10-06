using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TaskReadMigrationTests
{
    [Fact]
    public async Task Permission_seed_reapplies_and_rollback_preserves_custom_grants()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261004134916_TaskAssignmentHistory");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Module == "tasks" && p.Action == "read"));
                await db.Database.MigrateAsync();
                var grants = await (from rp in db.RolePermissions.IgnoreQueryFilters()
                    join p in db.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.Id
                    join r in db.Roles.IgnoreQueryFilters() on rp.RoleId equals r.Id
                    where p.Module == "tasks" && p.Action == "read"
                    select r.Name + ":" + p.Code).ToArrayAsync();
                Assert.Equal(new[] { "COMPANY_ADMIN:tasks.read.tenant", "EMPLOYEE:tasks.read.assigned", "EMPLOYEE:tasks.read.own",
                    "MANAGER:tasks.read.assigned", "MANAGER:tasks.read.managed", "MANAGER:tasks.read.own" }, grants.Order().ToArray());
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var role = Role.CreateCustom(seed.TenantId, "Task readers", DateTimeOffset.UtcNow);
                db.Roles.Add(role); await db.SaveChangesAsync();
                var permission = await db.Permissions.SingleAsync(p => p.Code == "tasks.read.assigned");
                db.RolePermissions.Add(new(role.Id, permission.Id)); await db.SaveChangesAsync();
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261004134916_TaskAssignmentHistory"));
                Assert.Equal("23514", error.SqlState);
            }
            await using (var db = database.Create(seed.UserId, seed.TenantId))
                Assert.Contains("20261005003438_ScopedTaskReadPermissions", await db.Database.GetAppliedMigrationsAsync());
        }
        finally { await database.DisposeAsync(); }
    }
}
