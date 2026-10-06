using System.Data;
using BizFlow.Application.Common;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TaskEvidencePersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Reports_and_rework_revisions_preserve_prior_evidence_and_approved_nullable_content()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var progress = TaskProgressReport.Create(a.Task, a.User, 0, null, now);
        var formal = TaskProgressReport.CreateFormalReport(a.Task, a.User, 100, "<b>Literal</b>\nReport", now.AddHours(1));
        var result = TaskResult.CreateSnapshot(a.Task, a.User, "Initial result", 1, now.AddHours(2));
        db.AddRange(progress, formal, result); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        // Test-only lifecycle fixtures; no execution path is exposed by persistence.
        await SetStateAsync(db, a.Task, TaskState.Submitted);
        await Check(() => InsertResultAsync(db, a.Task, a.User));
        await SetStateAsync(db, a.Task, TaskState.InProgress);
        db.Add(TaskResult.CreateSnapshot(a.Task, a.User, "Reworked result", 2, now.AddHours(3))); await db.SaveChangesAsync();
        var reports = await db.TaskProgressReports.OrderBy(r => r.SubmittedAt).ToListAsync();
        Assert.Equal(2, reports.Count); Assert.Null(reports[0].Content); Assert.Equal(0, reports[0].Percent);
        Assert.Equal(formal.Content, reports[1].Content); Assert.Equal(100, reports[1].Percent);
        var results = await db.TaskResults.OrderBy(r => r.RevisionNo).ToListAsync();
        Assert.Equal(2, results.Count); Assert.Equal(result.Id, results[0].Id); Assert.Equal(result.Content, results[0].Content);
        Assert.Equal(result.SubmittedAt, results[0].SubmittedAt); Assert.Equal(2, results[1].RevisionNo);
        // Storage preserves nullable content/defaults, not a claim of sufficient business evidence.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TaskResult" ("TaskResultId","TaskId","AuthorId","SubmittedAt") VALUES ({Guid.CreateVersion7()},{a.Task},{a.User},now())
            """);
        var nullable = await db.TaskResults.SingleAsync(r => r.Content == null); Assert.Equal(1, nullable.RevisionNo);
    }

    [Fact]
    public async Task Tenant_scope_author_ownership_and_forged_parents_fail_closed()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        db.AddRange(TaskProgressReport.Create(a.Task, a.User, 20, "Own", DateTimeOffset.UtcNow), TaskResult.CreateSnapshot(a.Task, a.User, "Own result", 1, DateTimeOffset.UtcNow));
        foreign.AddRange(TaskProgressReport.Create(b.Task, b.User, 20, "Foreign", DateTimeOffset.UtcNow), TaskResult.CreateSnapshot(b.Task, b.User, "Foreign result", 1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); await foreign.SaveChangesAsync();
        Assert.Single(await db.TaskProgressReports.ToListAsync()); Assert.Single(await db.TaskResults.ToListAsync());
        await using var anonymous = database.Create(null, a.Tenant); await using var platform = database.Create(database.PlatformUserId, null);
        foreach (var context in new[] { anonymous, platform })
        {
            Assert.Empty(await context.TaskProgressReports.ToListAsync()); Assert.Empty(await context.TaskResults.ToListAsync());
            context.Add(TaskProgressReport.Create(a.Task, a.User, 25, "Forbidden", DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<ApplicationFault>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
            context.Add(TaskResult.CreateSnapshot(a.Task, a.User, "Forbidden", 2, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<ApplicationFault>(() => context.SaveChangesAsync());
        }
        foreach (var invalid in new object[] {
            TaskProgressReport.Create(b.Task, a.User, 30, null, DateTimeOffset.UtcNow), TaskProgressReport.Create(a.Task, b.User, 30, null, DateTimeOffset.UtcNow),
            TaskResult.CreateSnapshot(b.Task, a.User, null, 2, DateTimeOffset.UtcNow), TaskResult.CreateSnapshot(a.Task, b.User, null, 2, DateTimeOffset.UtcNow) })
        { db.Add(invalid); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear(); }
        var forged = await foreign.WorkTasks.AsNoTracking().SingleAsync(); db.Entry(forged).Property(t => t.TenantId).CurrentValue = a.Tenant; db.Attach(forged);
        db.Add(TaskResult.CreateSnapshot(b.Task, a.User, "Forged parent", 2, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"DeletedAt\"=now() WHERE \"TaskId\"={a.Task}");
        Assert.Empty(await db.TaskProgressReports.ToListAsync()); Assert.Empty(await db.TaskResults.ToListAsync());
        Assert.Equal(1, await db.TaskResults.IgnoreQueryFilters().CountAsync(r => r.TaskId == a.Task));
        await Check(() => InsertResultAsync(db, a.Task, a.User)); await Check(() => InsertProgressAsync(db, a.Task, a.User));
    }

    [Fact]
    public async Task Database_and_EF_prevent_overwriting_or_deleting_evidence()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var progress = TaskProgressReport.Create(a.Task, a.User, 50, "Progress", DateTimeOffset.UtcNow);
        var result = TaskResult.CreateSnapshot(a.Task, a.User, "Result", 1, DateTimeOffset.UtcNow);
        db.AddRange(progress, result); await db.SaveChangesAsync();
        foreach (var entity in new object[] { progress, result })
        {
            db.ChangeTracker.Clear(); db.Attach(entity); db.Entry(entity).Property("Content").CurrentValue = "Overwrite";
            await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            db.Remove(entity); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
        }
        db.ChangeTracker.Clear();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskProgressReport\" SET \"Percent\"=60 WHERE \"ProgressReportId\"={progress.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskProgressReport\" SET \"Content\"=\"Content\" WHERE \"ProgressReportId\"={progress.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"TaskProgressReport\" WHERE \"ProgressReportId\"={progress.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskResult\" SET \"RevisionNo\"=2 WHERE \"TaskResultId\"={result.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskResult\" SET \"Content\"=\"Content\" WHERE \"TaskResultId\"={result.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"TaskResult\" WHERE \"TaskResultId\"={result.Id}"));
        await Check(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"TaskResult\", \"TaskProgressReport\""));
        Assert.Equal("Progress", (await db.TaskProgressReports.SingleAsync()).Content);
        Assert.Equal("Result", (await db.TaskResults.SingleAsync()).Content);
    }

    [Fact]
    public async Task Raw_writes_enforce_references_ranges_text_and_exact_result_submission_state()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await Check(() => InsertResultAsync(db, a.Task, b.User)); await Check(() => InsertProgressAsync(db, a.Task, b.User));
        await Check(() => InsertResultAsync(db, Guid.NewGuid(), a.User)); await Check(() => InsertProgressAsync(db, Guid.NewGuid(), a.User));
        await Check(() => InsertProgressAsync(db, a.Task, a.User, -1)); await Check(() => InsertProgressAsync(db, a.Task, a.User, 101));
        await Check(() => InsertResultAsync(db, a.Task, a.User, 0));
        await Check(() => InsertResultAsync(db, a.Task, a.User, content: "Bad\u0001text"));
        await Check(() => InsertProgressAsync(db, a.Task, a.User, content: "Bad\u007ftext"));
        foreach (var state in Enum.GetValues<TaskState>().Where(s => s != TaskState.InProgress))
        {
            await SetStateAsync(db, a.Task, state); await Check(() => InsertResultAsync(db, a.Task, a.User));
            db.Add(TaskResult.CreateSnapshot(a.Task, a.User, "Invalid state", 1, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        await SetStateAsync(db, a.Task, TaskState.InProgress); await InsertResultAsync(db, a.Task, a.User);
        Assert.Single(await db.TaskResults.ToListAsync());
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    public async Task Result_insert_observes_concurrent_overdue_change_even_with_old_snapshot(IsolationLevel isolation, string error)
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await using var writer = new NpgsqlConnection(database.ConnectionString); await writer.OpenAsync();
        await using var changer = new NpgsqlConnection(database.ConnectionString); await changer.OpenAsync();
        await using var writeTransaction = await writer.BeginTransactionAsync(isolation);
        await using var snapshot = new NpgsqlCommand("SELECT \"Title\" FROM \"Task\" WHERE \"TaskId\"=@id", writer, writeTransaction);
        snapshot.Parameters.AddWithValue("id", a.Task); Assert.NotNull(await snapshot.ExecuteScalarAsync());
        await using var changeTransaction = await changer.BeginTransactionAsync();
        await using var update = new NpgsqlCommand("UPDATE \"Task\" SET \"Status\"='OVERDUE' WHERE \"TaskId\"=@id", changer, changeTransaction);
        update.Parameters.AddWithValue("id", a.Task); await update.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", writer, writeTransaction);
        var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "TaskResult" ("TaskResultId","TaskId","AuthorId","SubmittedAt") VALUES (@id,@task,@user,now())
            """, writer, writeTransaction);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7()); insert.Parameters.AddWithValue("task", a.Task); insert.Parameters.AddWithValue("user", a.User);
        var pending = insert.ExecuteNonQueryAsync(); var blocked = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE pid={pid} AND wait_event_type='Lock'").SingleAsync() > 0;
            if (blocked) break;
            await Task.Delay(25);
        }
        await changeTransaction.CommitAsync(); Assert.Equal(error, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.True(blocked); Assert.Empty(await db.TaskResults.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_rollback_reapplies_but_either_kind_of_evidence_prevents_data_loss(bool result)
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261004081325_TaskDraftFoundation");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await SeedAsync(fixture); await using var db = fixture.Create(a.User, a.Tenant);
            if (result) await InsertResultAsync(db, a.Task, a.User); else await InsertProgressAsync(db, a.Task, a.User);
            await Check(() => migration.GetService<IMigrator>().MigrateAsync("20261004081325_TaskDraftFoundation"));
            Assert.Contains("20261004133416_TaskEvidenceHistory", await migration.Database.GetAppliedMigrationsAsync());
            Assert.Equal(result ? 1 : 0, await db.TaskResults.CountAsync());
            Assert.Equal(result ? 0 : 1, await db.TaskProgressReports.CountAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }

    private sealed record Seed(Guid Tenant, Guid User, Guid Task);
    private static async Task<Seed> SeedAsync(PostgresFixture fixture)
    {
        var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
        var task = WorkTask.CreateDraft(a.TenantId, a.UserId, "Task", DateTimeOffset.UtcNow); db.Add(task); await db.SaveChangesAsync();
        await SetStateAsync(db, task.Id, TaskState.InProgress); return new(a.TenantId, a.UserId, task.Id);
    }
    private static Task<int> SetStateAsync(BizFlowDbContext db, Guid task, TaskState state) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"={state} WHERE \"TaskId\"={task}");
    private static Task<int> InsertResultAsync(BizFlowDbContext db, Guid task, Guid author, int revision = 1, string? content = "Result") =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TaskResult" ("TaskResultId","TaskId","AuthorId","RevisionNo","Content","SubmittedAt")
            VALUES ({Guid.CreateVersion7()},{task},{author},{revision},{content},now())
            """);
    private static Task<int> InsertProgressAsync(BizFlowDbContext db, Guid task, Guid author, short percent = 0, string? content = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TaskProgressReport" ("ProgressReportId","TaskId","AuthorId","Percent","Content","SubmittedAt")
            VALUES ({Guid.CreateVersion7()},{task},{author},{percent},{content},now())
            """);
    private static async Task Check(Func<Task> action) => Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);
}
