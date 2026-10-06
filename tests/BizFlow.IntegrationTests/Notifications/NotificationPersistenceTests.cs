using BizFlow.Application.Common;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Notifications;

[Collection("Postgres")]
public sealed class NotificationPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Recipient_filters_and_write_guards_reject_foreign_and_detached_forged_receipts()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var other = UserAccount.CreateTenantUser(a.TenantId, "OTHER", "other@example.test", "Other", "fixture-only", DateTimeOffset.UtcNow);
        db.Users.Add(other); await db.SaveChangesAsync();
        var notification = Notification.Create(a.TenantId, a.UserId, NotificationEvent.TaskAssigned, "Own", "Content", "own");
        db.Notifications.Add(notification); await db.SaveChangesAsync();
        foreach (var hidden in new[] { database.Create(other.Id, a.TenantId), database.Create(b.UserId, b.TenantId), database.Create(database.PlatformUserId, null), database.Create(null, a.TenantId) })
        {
            await using (hidden)
            {
                Assert.Empty(await hidden.Notifications.ToListAsync());
                var forged = Notification.Create(a.TenantId, other.Id, NotificationEvent.TaskAssigned, "Own", "Content", "own");
                hidden.Entry(forged).Property(n => n.Id).CurrentValue = notification.Id;
                hidden.Attach(forged); forged.MarkRead(a.TenantId, other.Id, DateTimeOffset.UtcNow);
                await Assert.ThrowsAsync<ApplicationFault>(() => hidden.SaveChangesAsync());
            }
        }
        db.Notifications.Add(Notification.Create(a.TenantId, b.UserId, NotificationEvent.TaskAssigned, "Invalid", "Content", "cross"));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Null((await db.Notifications.SingleAsync()).ReadAt);
    }

    [Fact]
    public async Task SQL_constraints_preserve_tenant_recipients_deduplication_payload_and_first_receipt()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var notification = Notification.Create(a.TenantId, a.UserId, NotificationEvent.TaskAssigned, "Original", "Content", "dedup");
        db.Notifications.Add(notification); await db.SaveChangesAsync();
        db.Notifications.Add(Notification.Create(a.TenantId, a.UserId, NotificationEvent.TaskAssigned, "Duplicate", "Content", "dedup"));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.InnerException!).SqlState); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Notification" ("NotificationId","TenantId","RecipientId","Type","Title","Content","IdempotencyKey")
            VALUES ({Guid.NewGuid()},{a.TenantId},{b.UserId},'TaskAssigned','Invalid','Content','foreign');
            """));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Notification\" SET \"Title\"='Tampered' WHERE \"NotificationId\"={notification.Id}"));
        var read = await db.Notifications.SingleAsync(); read.MarkRead(a.TenantId, a.UserId, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Notification\" SET \"ReadAt\"=NULL WHERE \"NotificationId\"={notification.Id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Notification\" SET \"ReadAt\"=now() WHERE \"NotificationId\"={notification.Id}"));
        await using var otherDb = database.Create(b.UserId, b.TenantId);
        otherDb.Notifications.Add(Notification.Create(b.TenantId, b.UserId, NotificationEvent.TaskAssigned, "Other tenant", "Content", "dedup"));
        await otherDb.SaveChangesAsync(); Assert.Single(await otherDb.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Empty_migration_rolls_back_and_reapplies_but_populated_inbox_cannot_be_discarded()
    {
        var isolated = new PostgresFixture();
        try
        {
            await isolated.InitializeAsync();
            await using (var db = isolated.Create(isolated.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261001094232_AuditReadPermissions");
                Assert.DoesNotContain("20261001193054_NotificationInbox", await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            var seed = await isolated.SeedTenantAsync();
            await using var populated = isolated.Create(seed.UserId, seed.TenantId);
            var notification = Notification.Create(seed.TenantId, seed.UserId, NotificationEvent.Overdue, "Retain", "Content", "retain");
            populated.Notifications.Add(notification); await populated.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => populated.GetService<IMigrator>().MigrateAsync("20261001094232_AuditReadPermissions"));
            Assert.Contains("20261001193054_NotificationInbox", await populated.Database.GetAppliedMigrationsAsync());
            Assert.Equal(notification.Id, (await populated.Notifications.SingleAsync()).Id);
            await Assert.ThrowsAsync<PostgresException>(() => populated.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Notification\" SET \"Content\"='Tamper' WHERE \"NotificationId\"={notification.Id}"));
        }
        finally { await isolated.DisposeAsync(); }
    }
}
