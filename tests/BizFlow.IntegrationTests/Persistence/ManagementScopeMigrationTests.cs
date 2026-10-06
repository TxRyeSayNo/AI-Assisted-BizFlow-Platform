using BizFlow.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class ManagementScopeMigrationTests
{
    [Fact]
    public async Task Empty_scope_migration_rolls_back_and_reapplies_but_populated_configuration_is_preserved()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20260930175555_TenantRoleConfiguration");
                Assert.DoesNotContain("20261001012648_ManagementScopeFoundation", await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                Assert.Empty(await db.ManagementScopes.IgnoreQueryFilters().ToListAsync());
            }
            var seed = await database.SeedTenantAsync();
            Guid scopeId;
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var department = Department.Create(seed.TenantId, "OPS", "Operations", null, DateTimeOffset.UtcNow);
                db.Departments.Add(department); await db.SaveChangesAsync();
                var scope = ManagementScope.Create(seed.TenantId, seed.UserId, department.Id, true, seed.UserId, DateTimeOffset.UtcNow);
                scopeId = scope.Id; db.ManagementScopes.Add(scope); await db.SaveChangesAsync();
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260930175555_TenantRoleConfiguration"));
                Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            }
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                Assert.Contains("20261001012648_ManagementScopeFoundation", await db.Database.GetAppliedMigrationsAsync());
                Assert.Equal(scopeId, (await db.ManagementScopes.SingleAsync()).Id);
                // Failed rollback must also retain its database-side ownership trigger.
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "ManagementScope" SET "CreatedBy" = {database.PlatformUserId} WHERE "ManagementScopeId" = {scopeId}
                    """));
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
