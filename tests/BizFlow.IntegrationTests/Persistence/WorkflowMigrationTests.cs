using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class WorkflowMigrationTests
{
    [Fact]
    public async Task Empty_foundation_can_be_reapplied_but_populated_configuration_blocks_rollback()
    {
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001013618_UserDirectoryRead");
                Assert.DoesNotContain("20261001090500_WorkflowPersistenceFoundation", await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
            }
            var seed = await database.SeedTenantAsync();
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                await WorkflowPersistenceTests.SeedAsync(db, seed.TenantId);
                Assert.Equal(PostgresErrorCodes.CheckViolation,
                    (await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261001013618_UserDirectoryRead"))).SqlState);
            }
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                Assert.Contains("20261001090500_WorkflowPersistenceFoundation", await db.Database.GetAppliedMigrationsAsync());
                Assert.Single(await db.Workflows.ToListAsync()); Assert.Single(await db.WorkflowVersions.ToListAsync());
                Assert.Single(await db.WorkflowSteps.ToListAsync()); Assert.Single(await db.WorkflowTransitions.ToListAsync());
                Assert.Equal(PostgresErrorCodes.CheckViolation,
                    (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE \"Workflow\" CASCADE"))).SqlState);
            }
        }
        finally { await database.DisposeAsync(); }
    }
}
