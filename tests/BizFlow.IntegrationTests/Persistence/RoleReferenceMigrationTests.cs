using BizFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RoleReferenceMigrationTests
{
    [Fact]
    public async Task Role_reference_migration_reapplies_cleanly_and_refuses_destructive_assigned_rollback()
    {
        // Dedicated disposable database: rollback never touches other tests or a development tenant.
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20260930055252_PlatformRegistrationRead");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Code == "roles.configure"));
                await db.Database.MigrateAsync();
                Assert.Equal(4, await db.Roles.IgnoreQueryFilters().CountAsync(r => r.IsSystem));
                Assert.Equal(1, await db.Permissions.IgnoreQueryFilters().CountAsync(p => p.Code == "roles.configure"));
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
                db.UserRoles.Add(new UserRole(seed.UserId, role.Id)); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260930055252_PlatformRegistrationRead"));
            }
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                Assert.Contains("20260930175555_TenantRoleConfiguration", await db.Database.GetAppliedMigrationsAsync());
                Assert.Equal(1, await db.UserRoles.CountAsync(link => link.UserId == seed.UserId));
                Assert.Equal(1, await db.Permissions.CountAsync(p => p.Code == "roles.configure"));
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
