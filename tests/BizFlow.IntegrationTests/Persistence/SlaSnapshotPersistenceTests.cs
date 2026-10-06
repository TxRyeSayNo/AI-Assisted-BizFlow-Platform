using System.Data;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Domain.Sla;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class SlaSnapshotPersistenceTests(PostgresFixture database)
{
    private const string Hours = """{"monday":[{"start":"08:00","end":"17:30"}]}""";

    private static async Task<(SlaProfile Profile, BusinessCalendar Calendar)> SeedAsync(BizFlowDbContext db, Guid tenant)
    {
        var profile = SlaProfile.CreateDraft(tenant, "Snapshot fixture");
        var calendar = BusinessCalendar.Create(tenant, "UTC", Hours);
        db.AddRange(profile, calendar); await db.SaveChangesAsync(); return (profile, calendar);
    }

    [Fact]
    public async Task Reference_graph_is_tenant_scoped_and_forged_or_foreign_references_are_rejected()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var own = database.Create(a.UserId, a.TenantId);
        await using var foreign = database.Create(b.UserId, b.TenantId);
        var graph = await SeedAsync(own, a.TenantId); var other = await SeedAsync(foreign, b.TenantId);
        own.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 1, 60, 45, graph.Calendar.Id)); await own.SaveChangesAsync();
        foreach (var context in new[] { database.Create(b.UserId, b.TenantId), database.Create(null, a.TenantId), database.Create(database.PlatformUserId, null) })
        {
            await using var db = context;
            Assert.False(await db.SlaVersions.AnyAsync(v => v.SlaProfileId == graph.Profile.Id));
            Assert.False(await db.BusinessCalendars.AnyAsync(c => c.Id == graph.Calendar.Id));
            db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 2, 60, 45, graph.Calendar.Id));
            await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        own.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 2, 60, 45, other.Calendar.Id));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        var forged = BusinessCalendar.Create(a.TenantId, "UTC", Hours);
        own.Entry(forged).Property(c => c.Id).CurrentValue = other.Calendar.Id; own.Attach(forged);
        own.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 2, 60, 45, other.Calendar.Id));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync());
        await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(own, graph.Profile.Id, other.Calendar.Id, 2));
        Assert.Single(await own.SlaVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Saved_versions_and_referenced_calendar_values_are_immutable_in_EF_and_SQL()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        var version = SlaVersion.CreateSnapshot(graph.Profile.Id, 1, 60, 45, graph.Calendar.Id);
        db.SlaVersions.Add(version); await db.SaveChangesAsync();
        db.Entry(version).Property(v => v.TargetMinutes).CurrentValue = 90;
        Assert.Equal("SLA.SNAPSHOT_IMMUTABLE", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code); db.ChangeTracker.Clear();
        var calendar = await db.BusinessCalendars.SingleAsync(); db.Entry(calendar).Property(c => c.TimeZone).CurrentValue = "Europe/Paris";
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.SlaVersions.Remove(version); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        FormattableString[] changes = [
            $"""UPDATE "SLAVersion" SET "TargetMinutes"=90 WHERE "SLAVersionId"={version.Id}""",
            $"""UPDATE "SLAVersion" SET "VersionNo"=2 WHERE "SLAVersionId"={version.Id}""",
            $"""UPDATE "SLAVersion" SET "EscalationConfigJson"=jsonb_build_object() WHERE "SLAVersionId"={version.Id}""",
            $"""DELETE FROM "SLAVersion" WHERE "SLAVersionId"={version.Id}""",
            $"""UPDATE "BusinessCalendar" SET "TimeZone"='Europe/Paris' WHERE "CalendarId"={calendar.Id}""",
            $"""UPDATE "BusinessCalendar" SET "WorkingHoursJson"=jsonb_build_object() WHERE "CalendarId"={calendar.Id}""",
            $"""UPDATE "BusinessCalendar" SET "HolidaysJson"='["2026-12-25"]'::jsonb WHERE "CalendarId"={calendar.Id}""",
            $"""DELETE FROM "BusinessCalendar" WHERE "CalendarId"={calendar.Id}"""
        ];
        foreach (var change in changes) Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(change))).SqlState);
        // New version may reuse the same frozen calendar without changing its business fields.
        db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 2, 90, 60, calendar.Id)); await db.SaveChangesAsync();
        Assert.Equal(2, await db.SlaVersions.CountAsync()); Assert.Equal("UTC", (await db.BusinessCalendars.SingleAsync()).TimeZone);
    }

    [Fact]
    public async Task SQL_validates_calendar_schema_and_snapshot_thresholds_and_empty_schedules()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        foreach (var hours in new[] { "[]", "null", "{\"monday\":{}}", "{\"Monday\":[]}",
            "{\"monday\":[{\"start\":\"22:00\",\"end\":\"02:00\"}]}", "{\"monday\":[{\"start\":\"08:00\",\"end\":\"24:01\"}]}",
            "{\"monday\":[{\"start\":\"08:00\",\"end\":\"12:00\"},{\"start\":\"11:00\",\"end\":\"17:00\"}]}" })
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BusinessCalendar\" SET \"WorkingHoursJson\"={hours}::jsonb WHERE \"CalendarId\"={graph.Calendar.Id}"));
        foreach (var holidays in new[] { "{}", "[\"2026-02-29\"]", "[\"2026-1-01\"]", "[\"2028-02-29\",\"2028-02-29\"]", "[123]" })
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BusinessCalendar\" SET \"HolidaysJson\"={holidays}::jsonb WHERE \"CalendarId\"={graph.Calendar.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BusinessCalendar\" SET \"HolidaysJson\"='[\"2028-02-29\"]'::jsonb WHERE \"CalendarId\"={graph.Calendar.Id}");
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BusinessCalendar\" SET \"TimeZone\"='Not/AZone' WHERE \"CalendarId\"={graph.Calendar.Id}"));
        foreach (var (target, warning) in new[] { (0, 0), (60, -1), (60, 60), (60, 61) })
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1, target, warning));
        var empty = BusinessCalendar.Create(a.TenantId); db.BusinessCalendars.Add(empty); await db.SaveChangesAsync();
        db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 1, 60, 45, empty.Id));
        Assert.Equal("SLA.INVALID_CALENDAR", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, empty.Id, 1));
        await InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1))).SqlState);
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    public async Task Calendar_editor_cannot_evade_freeze_with_a_concurrent_or_older_snapshot(IsolationLevel isolation, string sqlState)
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        await using var creator = new NpgsqlConnection(database.ConnectionString); await creator.OpenAsync();
        await using var editor = new NpgsqlConnection(database.ConnectionString); await editor.OpenAsync();
        await using var editTransaction = await editor.BeginTransactionAsync(isolation);
        await using var snapshot = new NpgsqlCommand("SELECT \"TimeZone\" FROM \"BusinessCalendar\" WHERE \"CalendarId\"=@id", editor, editTransaction);
        snapshot.Parameters.AddWithValue("id", graph.Calendar.Id); Assert.Equal("UTC", await snapshot.ExecuteScalarAsync());
        await using var createTransaction = await creator.BeginTransactionAsync();
        await using var insert = VersionCommand(creator, createTransaction, graph.Profile.Id, graph.Calendar.Id);
        await insert.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", editor, editTransaction);
        var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var update = new NpgsqlCommand("UPDATE \"BusinessCalendar\" SET \"TimeZone\"='Europe/Paris' WHERE \"CalendarId\"=@id", editor, editTransaction);
        update.Parameters.AddWithValue("id", graph.Calendar.Id); var pending = update.ExecuteNonQueryAsync();
        var blocked = await WaitForLockAsync(db, pid, pending);
        await createTransaction.CommitAsync();
        Assert.Equal(sqlState, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.True(blocked, "Calendar writes must serialize against version reference creation.");
        Assert.Equal("UTC", (await db.BusinessCalendars.AsNoTracking().SingleAsync()).TimeZone);
        Assert.Single(await db.SlaVersions.ToListAsync());
    }

    [Fact]
    public async Task Version_waits_for_an_earlier_calendar_edit_then_freezes_the_committed_configuration()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        await using var editor = new NpgsqlConnection(database.ConnectionString); await editor.OpenAsync();
        await using var creator = new NpgsqlConnection(database.ConnectionString); await creator.OpenAsync();
        await using var transaction = await editor.BeginTransactionAsync();
        await using var edit = new NpgsqlCommand("UPDATE \"BusinessCalendar\" SET \"TimeZone\"='Europe/Paris' WHERE \"CalendarId\"=@id", editor, transaction);
        edit.Parameters.AddWithValue("id", graph.Calendar.Id); await edit.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", creator); var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = VersionCommand(creator, null, graph.Profile.Id, graph.Calendar.Id);
        var pending = insert.ExecuteNonQueryAsync(); var blocked = await WaitForLockAsync(db, pid, pending);
        await transaction.CommitAsync(); Assert.Equal(1, await pending); Assert.True(blocked);
        Assert.Equal("Europe/Paris", (await db.BusinessCalendars.AsNoTracking().SingleAsync()).TimeZone);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"BusinessCalendar\" SET \"TimeZone\"='UTC' WHERE \"CalendarId\"={graph.Calendar.Id}"));
    }

    [Fact]
    public async Task Empty_migration_reapplies_but_populated_calendar_history_prevents_rollback()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync();
            await using (var migrationDb = fixture.Create(null, null))
            {
                await migrationDb.GetService<IMigrator>().MigrateAsync("20261002125924_SlaProfileCatalog");
                await migrationDb.Database.MigrateAsync(); await migrationDb.Database.MigrateAsync();
            }
            var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
            db.BusinessCalendars.Add(BusinessCalendar.Create(a.TenantId)); await db.SaveChangesAsync();
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() =>
                db.GetService<IMigrator>().MigrateAsync("20261002125924_SlaProfileCatalog"))).SqlState);
            Assert.Single(await db.BusinessCalendars.ToListAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Escalation_schema_and_live_recipient_tenancy_are_enforced_in_EF_and_SQL()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId); var graph = await SeedAsync(db, a.TenantId);
        static string Config(Guid recipient) => JsonSerializer.Serialize(new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { recipient } } } });
        foreach (var recipient in new[] { b.UserId, database.PlatformUserId, Guid.NewGuid() })
        {
            var json = Config(recipient); db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 1, 60, 45, graph.Calendar.Id, json));
            Assert.Equal("SLA.INVALID_ESCALATION_TARGET", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1, escalation: json));
        }
        foreach (var json in new[] { "{}", "{\"levels\":{}}", "{\"levels\":[{}]}", "{\"levels\":[],\"extra\":true}" })
            await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1, escalation: json));
        var repeated = JsonSerializer.Serialize(new { levels = new[] {
            new { level = 1, afterMinutes = 0, recipientUserIds = new[] { a.UserId } },
            new { level = 2, afterMinutes = 0, recipientUserIds = new[] { a.UserId } } } });
        await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 1, escalation: repeated));
        var valid = Config(a.UserId); db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 1, 60, 45, graph.Calendar.Id, valid)); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={a.UserId}");
        db.SlaVersions.Add(SlaVersion.CreateSnapshot(graph.Profile.Id, 2, 60, 45, graph.Calendar.Id, valid));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => InsertVersionAsync(db, graph.Profile.Id, graph.Calendar.Id, 2, escalation: valid));
        Assert.Equal(a.UserId, Assert.Single((await db.SlaVersions.SingleAsync()).EscalationRecipientIds())); // History remains intact.
    }

    [Fact]
    public async Task Version_creation_waits_for_recipient_deactivation_and_rechecks_eligibility()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId); var graph = await SeedAsync(db, a.TenantId);
        await using var editor = new NpgsqlConnection(database.ConnectionString); await editor.OpenAsync();
        await using var creator = new NpgsqlConnection(database.ConnectionString); await creator.OpenAsync();
        await using var transaction = await editor.BeginTransactionAsync();
        await using var edit = new NpgsqlCommand("UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"=@id", editor, transaction);
        edit.Parameters.AddWithValue("id", a.UserId); await edit.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", creator); var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = VersionCommand(creator, null, graph.Profile.Id, graph.Calendar.Id);
        insert.CommandText = """
            INSERT INTO "SLAVersion" ("SLAVersionId","SLAProfileId","VersionNo","TargetMinutes","WarningMinutes","CalendarId","EscalationConfigJson")
            VALUES (@id,@profile,1,60,45,@calendar,@config::jsonb)
            """;
        insert.Parameters.AddWithValue("config", JsonSerializer.Serialize(new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { a.UserId } } } }));
        var pending = insert.ExecuteNonQueryAsync(); var blocked = await WaitForLockAsync(db, pid, pending);
        await transaction.CommitAsync();
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.True(blocked); Assert.Empty(await db.SlaVersions.ToListAsync());
    }

    private static Task<int> InsertVersionAsync(BizFlowDbContext db, Guid profile, Guid calendar, int number, int target = 60, int warning = 45, string? escalation = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SLAVersion" ("SLAVersionId","SLAProfileId","VersionNo","TargetMinutes","WarningMinutes","CalendarId","EscalationConfigJson")
            VALUES ({Guid.CreateVersion7()},{profile},{number},{target},{warning},{calendar},{escalation}::jsonb)
            """);
    private static NpgsqlCommand VersionCommand(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid profile, Guid calendar)
    {
        var command = new NpgsqlCommand("""
            INSERT INTO "SLAVersion" ("SLAVersionId","SLAProfileId","VersionNo","TargetMinutes","WarningMinutes","CalendarId")
            VALUES (@id,@profile,1,60,45,@calendar)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7()); command.Parameters.AddWithValue("profile", profile); command.Parameters.AddWithValue("calendar", calendar);
        return command;
    }
    private static async Task<bool> WaitForLockAsync(BizFlowDbContext observer, int pid, Task command)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !command.IsCompleted)
        {
            if (await observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE pid={pid} AND wait_event_type='Lock'").SingleAsync() > 0) return true;
            await Task.Delay(25);
        }
        return false;
    }
}
