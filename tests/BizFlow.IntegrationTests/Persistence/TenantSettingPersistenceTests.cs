using BizFlow.Application.Common;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TenantSettingPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Settings_are_tenant_filtered_and_editors_cannot_be_spoofed()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var setting = TenantSetting.Create(a.TenantId, "ai.enabled", "false", a.UserId, DateTimeOffset.UtcNow);
        db.TenantSettings.Add(setting); await db.SaveChangesAsync();
        Assert.Equal(setting.Id, (await db.TenantSettings.SingleAsync()).Id);
        await using var other = database.Create(b.UserId, b.TenantId);
        await using var anonymous = database.Create(null, a.TenantId);
        await using var platform = database.Create(database.PlatformUserId, null);
        Assert.Empty(await other.TenantSettings.ToListAsync()); Assert.Empty(await anonymous.TenantSettings.ToListAsync());
        Assert.Empty(await platform.TenantSettings.ToListAsync());
        db.TenantSettings.Add(TenantSetting.Create(a.TenantId, "notification.email_enabled", "true", b.UserId, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var colleague = UserAccount.CreateTenantUser(a.TenantId, "SETTING-EDITOR", "setting-editor@example.test", "Editor", "test-only", DateTimeOffset.UtcNow);
        db.Users.Add(colleague); await db.SaveChangesAsync();
        db.TenantSettings.Add(TenantSetting.Create(a.TenantId, "notification.email_enabled", "true", colleague.Id, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        platform.TenantSettings.Add(TenantSetting.Create(a.TenantId, "notification.email_enabled", "true", database.PlatformUserId, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => platform.SaveChangesAsync());
        // A dedicated, authorized provisioning path is required; platform presence is not implicit tenant write authority.
    }

    [Fact]
    public async Task Forged_detached_updates_cannot_change_foreign_settings_and_stale_writes_conflict()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var own = database.Create(a.UserId, a.TenantId);
        await using var foreign = database.Create(b.UserId, b.TenantId);
        var setting = TenantSetting.Create(b.TenantId, "ai.enabled", "false", b.UserId, DateTimeOffset.UtcNow);
        foreign.TenantSettings.Add(setting); await foreign.SaveChangesAsync();
        var forged = TenantSetting.Create(a.TenantId, "ai.enabled", "true", a.UserId, DateTimeOffset.UtcNow);
        own.Entry(forged).Property(s => s.Id).CurrentValue = setting.Id;
        own.Attach(forged); own.Entry(forged).Property(s => s.ValueJson).IsModified = true;
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync());
        await using var concurrent = database.Create(b.UserId, b.TenantId);
        var stale = await concurrent.TenantSettings.SingleAsync();
        setting.SetValue(b.TenantId, "true", b.UserId, DateTimeOffset.UtcNow); await foreign.SaveChangesAsync();
        stale.SetValue(b.TenantId, "false", b.UserId, DateTimeOffset.UtcNow.AddMinutes(1));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => concurrent.SaveChangesAsync());
        foreign.ChangeTracker.Clear(); Assert.Equal("true", (await foreign.TenantSettings.SingleAsync()).ValueJson);
    }

    [Fact]
    public async Task SQL_enforces_key_types_limits_unique_tenant_key_and_reference_identity()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var setting = TenantSetting.Create(a.TenantId, "attachment.max_size_bytes", "524288000", a.UserId, DateTimeOffset.UtcNow);
        db.TenantSettings.Add(setting); await db.SaveChangesAsync();
        foreach (var invalid in new[] { ("arbitrary.policy", "true"), ("ai.enabled", "\"true\""),
            ("ai.enabled", "null"), ("attachment.max_size_bytes", "524288001"), ("attachment.max_size_bytes", "-1"),
            ("ai.auto_action_tools", "[1]"), ("ai.auto_action_tools", "[\"\\t\"]"), ("report.max_range_days", "1.5"),
            ("report.max_range_days", "0"), ("ai.confidence_threshold", "1.01") })
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(db, a.TenantId, a.UserId, invalid.Item1, invalid.Item2))).SqlState);
        Assert.Equal(PostgresErrorCodes.UniqueViolation,
            (await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(db, a.TenantId, a.UserId, setting.Key, "1024"))).SqlState);
        await InsertAsync(db, b.TenantId, b.UserId, setting.Key, "1024"); // Separate tenant may configure the same key.
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(db, a.TenantId, b.UserId, "ai.enabled", "true"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "TenantSetting" SET "Key"='ai.monthly_call_limit' WHERE "TenantSettingId"={setting.Id}
            """));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "TenantSetting" SET "TenantId"={b.TenantId}, "UpdatedBy"={b.UserId} WHERE "TenantSettingId"={setting.Id}
            """));
        db.TenantSettings.Remove(setting); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Empty_migration_can_roll_back_but_populated_policies_and_constraints_are_preserved()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync();
            await using (var db = fixture.Create(null, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001193054_NotificationInbox");
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            var seed = await fixture.SeedTenantAsync();
            await using var tenant = fixture.Create(seed.UserId, seed.TenantId);
            tenant.TenantSettings.Add(TenantSetting.Create(seed.TenantId, "ai.auto_action_enabled", "false", seed.UserId, DateTimeOffset.UtcNow));
            await tenant.SaveChangesAsync();
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() =>
                tenant.GetService<IMigrator>().MigrateAsync("20261001193054_NotificationInbox"))).SqlState);
            Assert.Contains("20261002123538_TenantSettingFoundation", await tenant.Database.GetAppliedMigrationsAsync());
            Assert.Single(await tenant.TenantSettings.ToListAsync());
            await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(tenant, seed.TenantId, seed.UserId, "attachment.max_size_bytes", "524288001"));
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static Task InsertAsync(BizFlowDbContext db, Guid tenantId, Guid editorId, string key, string json) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TenantSetting" ("TenantSettingId", "TenantId", "Key", "ValueJson", "UpdatedBy", "UpdatedAt")
            VALUES ({Guid.CreateVersion7()}, {tenantId}, {key}, {json}::jsonb, {editorId}, {DateTimeOffset.UtcNow})
            """);
}
