using System.Data;
using BizFlow.Application.Common;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RequestResolutionPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Evidence_is_tenant_scoped_and_rework_appends_without_overwriting_prior_resolution()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        var first = RequestResolution.Create(a.Request, a.User, "Original\nresolution", 1, DateTimeOffset.UtcNow);
        db.RequestResolutions.Add(first); await db.SaveChangesAsync(); await db.Entry(first).ReloadAsync();
        foreign.RequestResolutions.Add(RequestResolution.Create(b.Request, b.User, "Other tenant", 1, DateTimeOffset.UtcNow)); await foreign.SaveChangesAsync();
        // Test-only lifecycle fixtures until the authorized workflow engine is implemented.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='RESOLVED' WHERE \"RequestId\"={a.Request}");
        await Check(() => InsertAsync(db, a.Request, a.User));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='IN_PROGRESS' WHERE \"RequestId\"={a.Request}");
        var second = RequestResolution.Create(a.Request, a.User, "Reworked resolution", 2, DateTimeOffset.UtcNow);
        db.RequestResolutions.Add(second); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var history = await db.RequestResolutions.OrderBy(r => r.RevisionNo).ToListAsync();
        Assert.Equal(2, history.Count); Assert.Equal(first.Id, history[0].Id);
        Assert.Equal(first.Content, history[0].Content); Assert.Equal(first.CreatedAt, history[0].CreatedAt);
        Assert.Equal(second.Id, history[1].Id); Assert.Single(await foreign.RequestResolutions.ToListAsync());
        await using var anonymous = database.Create(null, a.Tenant); await using var platform = database.Create(database.PlatformUserId, null);
        Assert.Empty(await anonymous.RequestResolutions.ToListAsync()); Assert.Empty(await platform.RequestResolutions.ToListAsync());
        foreach (var context in new[] { anonymous, platform })
        {
            context.RequestResolutions.Add(RequestResolution.Create(a.Request, a.User, "Forbidden", 3, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<ApplicationFault>(() => context.SaveChangesAsync());
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"DeletedAt\"=now() WHERE \"RequestId\"={a.Request}");
        Assert.Empty(await db.RequestResolutions.ToListAsync());
        Assert.Equal(2, await db.RequestResolutions.IgnoreQueryFilters().CountAsync(r => r.RequestId == a.Request));
        await Check(() => InsertAsync(db, a.Request, a.User));
    }

    [Fact]
    public async Task Persisted_ownership_not_attached_objects_controls_writes_and_history_cannot_be_edited()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        foreach (var invalid in new[] { RequestResolution.Create(b.Request, a.User, "Foreign parent", 1, DateTimeOffset.UtcNow),
            RequestResolution.Create(a.Request, b.User, "Foreign resolver", 1, DateTimeOffset.UtcNow) })
        {
            db.RequestResolutions.Add(invalid); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        var forged = await foreign.Requests.AsNoTracking().SingleAsync();
        db.Entry(forged).Property(r => r.TenantId).CurrentValue = a.Tenant; db.Attach(forged);
        db.RequestResolutions.Add(RequestResolution.Create(b.Request, a.User, "Forged parent", 1, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var resolution = RequestResolution.Create(a.Request, a.User, "Immutable", 1, DateTimeOffset.UtcNow);
        db.Add(resolution); await db.SaveChangesAsync();
        db.Entry(resolution).Property(r => r.Content).CurrentValue = "Replaced";
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Remove(resolution); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        foreach (var sql in new[] {
            "UPDATE \"RequestResolution\" SET \"Content\"='Replaced' WHERE \"ResolutionId\"={0}",
            "UPDATE \"RequestResolution\" SET \"RevisionNo\"=2 WHERE \"ResolutionId\"={0}",
            "UPDATE \"RequestResolution\" SET \"Content\"=\"Content\" WHERE \"ResolutionId\"={0}",
            "DELETE FROM \"RequestResolution\" WHERE \"ResolutionId\"={0}" })
            await Check(() => db.Database.ExecuteSqlRawAsync(sql, resolution.Id));
        await Check(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"RequestResolution\""));
        Assert.Equal("Immutable", (await db.RequestResolutions.SingleAsync()).Content);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='REJECTED' WHERE \"RequestId\"={a.Request}");
        await Check(() => InsertAsync(db, a.Request, a.User));
        Assert.Single(await db.RequestResolutions.ToListAsync());
    }

    [Fact]
    public async Task Raw_sql_validates_tenant_references_content_revision_and_every_nonprocessing_state()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await Check(() => InsertAsync(db, a.Request, b.User)); await Check(() => InsertAsync(db, Guid.NewGuid(), a.User));
        foreach (var text in new[] { "", " \r\n\t ", "Bad\u0001text", "Bad\u007ftext" })
            await Check(() => InsertAsync(db, a.Request, a.User, text));
        await Check(() => InsertAsync(db, a.Request, a.User, revision: 0));
        await Check(() => InsertAsync(db, a.Request, a.User, revision: -1));
        // REJECTED is last because its original row is permanently immutable.
        foreach (var state in Enum.GetValues<RequestState>().Where(s => s is not (RequestState.InProgress or RequestState.Rejected)).Append(RequestState.Rejected))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"={state} WHERE \"RequestId\"={a.Request}");
            await Check(() => InsertAsync(db, a.Request, a.User));
        }
        Assert.Empty(await db.RequestResolutions.ToListAsync());
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    public async Task Evidence_waits_for_parent_lifecycle_change_and_cannot_use_an_older_snapshot(IsolationLevel isolation, string error)
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await using var writer = new NpgsqlConnection(database.ConnectionString); await writer.OpenAsync();
        await using var changer = new NpgsqlConnection(database.ConnectionString); await changer.OpenAsync();
        await using var writeTransaction = await writer.BeginTransactionAsync(isolation);
        await using var snapshot = new NpgsqlCommand("SELECT \"Title\" FROM \"Request\" WHERE \"RequestId\"=@id", writer, writeTransaction);
        snapshot.Parameters.AddWithValue("id", a.Request); Assert.NotNull(await snapshot.ExecuteScalarAsync());
        await using var changeTransaction = await changer.BeginTransactionAsync();
        await using var update = new NpgsqlCommand("UPDATE \"Request\" SET \"Status\"='REJECTED' WHERE \"RequestId\"=@id", changer, changeTransaction);
        update.Parameters.AddWithValue("id", a.Request); await update.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", writer, writeTransaction);
        var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "RequestResolution" ("ResolutionId","RequestId","ResolverId","Content","CreatedAt")
            VALUES (@id,@request,@user,'Stale resolution',now())
            """, writer, writeTransaction);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7()); insert.Parameters.AddWithValue("request", a.Request); insert.Parameters.AddWithValue("user", a.User);
        var pending = insert.ExecuteNonQueryAsync(); var blocked = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE pid={pid} AND wait_event_type='Lock'").SingleAsync() > 0;
            if (blocked) break;
            await Task.Delay(25);
        }
        await changeTransaction.CommitAsync();
        Assert.Equal(error, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.True(blocked, "Evidence insertion must lock its request against lifecycle changes.");
        Assert.Empty(await db.RequestResolutions.ToListAsync());
    }

    [Fact]
    public async Task Empty_rollback_reapplies_but_existing_resolution_history_blocks_rollback()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261003172253_ServiceMetadataUpdatePermission");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await SeedAsync(fixture); await using var db = fixture.Create(a.User, a.Tenant);
            await InsertAsync(db, a.Request, a.User);
            await Check(() => migration.GetService<IMigrator>().MigrateAsync("20261003172253_ServiceMetadataUpdatePermission"));
            Assert.Contains("20261004075754_RequestResolutionHistory", await migration.Database.GetAppliedMigrationsAsync());
            Assert.Equal(1, (await db.RequestResolutions.SingleAsync()).RevisionNo);
        }
        finally { await fixture.DisposeAsync(); }
    }

    private sealed record Seed(Guid Tenant, Guid User, Guid Request);
    private static async Task<Seed> SeedAsync(PostgresFixture fixture)
    {
        var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
        var service = InternalService.Create(a.TenantId, "IT", "Support", null, true, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, "GENERAL", "General"); db.AddRange(service, category); await db.SaveChangesAsync();
        var request = WorkRequest.CreateDraft(a.TenantId, a.UserId, service.Id, category.Id, "Request", "Details", DateTimeOffset.UtcNow);
        db.Add(request); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='IN_PROGRESS' WHERE \"RequestId\"={request.Id}");
        return new(a.TenantId, a.UserId, request.Id);
    }
    private static Task<int> InsertAsync(BizFlowDbContext db, Guid request, Guid user, string content = "Details", int revision = 1) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RequestResolution" ("ResolutionId","RequestId","ResolverId","Content","RevisionNo","CreatedAt")
            VALUES ({Guid.CreateVersion7()},{request},{user},{content},{revision},now())
            """);
    private static async Task Check(Func<Task> action) => Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);
}
