using System.Data;
using BizFlow.Application.Common;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TaskAssignmentPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Targets_round_trip_and_closed_assignment_chain_preserves_original_history()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var first = TaskAssignment.Create(a.Task, a.User, a.Department, null, DateTimeOffset.UtcNow);
        db.Add(first); await db.SaveChangesAsync(); await db.Entry(first).ReloadAsync();
        Assert.Null(first.UserId); Assert.Equal(a.Department, first.DepartmentId); Assert.Null(first.AcceptedAt);
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"UserId\"={a.User} WHERE \"TaskAssignmentId\"={first.Id}"));
        // Test-only history fixtures. Production reassignment/acceptance still require use cases and audit.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"EndedAt\"=now() WHERE \"TaskAssignmentId\"={first.Id}");
        var next = TaskAssignment.Create(a.Task, a.User, a.Department, a.User, DateTimeOffset.UtcNow);
        db.Add(next); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var history = await db.TaskAssignments.OrderBy(x => x.AssignedAt).ToListAsync();
        Assert.Equal(2, history.Count); Assert.Equal(first.Id, history[0].Id); Assert.Equal(first.AssignedAt, history[0].AssignedAt);
        Assert.Null(history[0].UserId); Assert.NotNull(history[0].EndedAt); Assert.Equal(next.Id, history[1].Id); Assert.Null(history[1].EndedAt);
        Assert.Equal(a.User, history[1].UserId); Assert.Equal(a.Department, history[1].DepartmentId);
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"EndedAt\"=NULL WHERE \"TaskAssignmentId\"={first.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AssignedAt\"=\"AssignedAt\" WHERE \"TaskAssignmentId\"={first.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"TaskAssignment\" WHERE \"TaskAssignmentId\"={first.Id}"));
        await Check(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"TaskAssignment\""));
    }

    [Fact]
    public async Task Tenant_context_live_targets_and_forged_parents_cannot_bypass_initial_write_guards()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        db.Add(New(a)); foreign.Add(New(b)); await db.SaveChangesAsync(); await foreign.SaveChangesAsync();
        Assert.Single(await db.TaskAssignments.ToListAsync()); Assert.Single(await foreign.TaskAssignments.ToListAsync());
        await using var anonymous = database.Create(null, a.Tenant); await using var platform = database.Create(database.PlatformUserId, null);
        foreach (var context in new[] { anonymous, platform })
        {
            Assert.Empty(await context.TaskAssignments.ToListAsync()); context.Add(New(a));
            await Assert.ThrowsAsync<ApplicationFault>(() => context.SaveChangesAsync());
        }
        foreach (var invalid in new[] { New(a with { Task = b.Task }), New(a with { Department = b.Department }),
            TaskAssignment.Create(a.Task, b.User, a.Department, a.User, DateTimeOffset.UtcNow), TaskAssignment.Create(a.Task, a.User, a.Department, b.User, DateTimeOffset.UtcNow) })
        { db.Add(invalid); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear(); }
        var forged = await foreign.WorkTasks.AsNoTracking().SingleAsync(); db.Entry(forged).Property(t => t.TenantId).CurrentValue = a.Tenant; db.Attach(forged);
        db.Add(New(a with { Task = b.Task })); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Department\" SET \"Status\"='INACTIVE' WHERE \"DepartmentId\"={a.Department}");
        db.Add(New(a)); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var stored = await db.TaskAssignments.SingleAsync(); db.Entry(stored).Property(x => x.EndedAt).CurrentValue = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"DeletedAt\"=now() WHERE \"TaskId\"={a.Task}");
        Assert.Empty(await db.TaskAssignments.ToListAsync()); Assert.Equal(1, await db.TaskAssignments.IgnoreQueryFilters().CountAsync(x => x.TaskId == a.Task));
    }

    [Fact]
    public async Task Receipt_is_set_once_with_reason_consistent_times_and_assigned_parent_state()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var item = New(a); db.Add(item); await db.SaveChangesAsync();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=now() WHERE \"TaskAssignmentId\"={item.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"={a.Task}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectedAt\"=now() WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectedAt\"=now(),\"RejectionReason\"={"\r\n\t "} WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectedAt\"=now(),\"RejectionReason\"={"Bad\u0001text"} WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=\"AssignedAt\" - interval '1 second' WHERE \"TaskAssignmentId\"={item.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectedAt\"=now(),\"RejectionReason\"='Cannot accept' WHERE \"TaskAssignmentId\"={item.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectionReason\"='Rewritten' WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=now() WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"RejectedAt\"=NULL,\"RejectionReason\"=NULL WHERE \"TaskAssignmentId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"EndedAt\"=\"AssignedAt\" - interval '1 second' WHERE \"TaskAssignmentId\"={item.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"EndedAt\"=now() WHERE \"TaskAssignmentId\"={item.Id}");
        var next = New(a); db.Add(next); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=now() WHERE \"TaskAssignmentId\"={next.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=NULL WHERE \"TaskAssignmentId\"={next.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=now() WHERE \"TaskAssignmentId\"={next.Id}"));
    }

    [Fact]
    public async Task Raw_sql_cannot_reference_foreign_or_inactive_targets_or_create_on_terminal_task()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await Check(() => InsertAsync(db, a.Task, b.User, a.Department, a.User));
        await Check(() => InsertAsync(db, a.Task, a.User, b.Department, a.User));
        await Check(() => InsertAsync(db, a.Task, a.User, a.Department, b.User));
        await Check(() => InsertAsync(db, a.Task, a.User, null, null));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Department\" SET \"Status\"='INACTIVE' WHERE \"DepartmentId\"={a.Department}");
        await Check(() => InsertAsync(db, a.Task, a.User, a.Department, null));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={a.User}");
        await Check(() => InsertAsync(db, a.Task, a.User, null, a.User));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='ACTIVE' WHERE \"UserId\"={a.User}");
        foreach (var status in new[] { TaskState.Completed, TaskState.Cancelled })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"={status} WHERE \"TaskId\"={a.Task}");
            await Check(() => InsertAsync(db, a.Task, a.User, null, a.User));
            db.Add(TaskAssignment.Create(a.Task, a.User, null, a.User, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task Concurrent_initial_assignments_have_exactly_one_current_winner()
    {
        var a = await SeedAsync(database);
        async Task<bool> Attempt()
        {
            await using var db = database.Create(a.User, a.Tenant); db.Add(New(a));
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
        }
        var outcomes = await Task.WhenAll(Attempt(), Attempt()); Assert.Single(outcomes, x => x);
        await using var reader = database.Create(a.User, a.Tenant); Assert.Single(await reader.TaskAssignments.ToListAsync());
    }

    [Theory]
    [InlineData(false, IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(false, IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(false, IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    [InlineData(true, IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(true, IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(true, IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    public async Task Assignment_observes_concurrent_target_deactivation(bool department, IsolationLevel isolation, string error)
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        await using var writer = new NpgsqlConnection(database.ConnectionString); await writer.OpenAsync();
        await using var changer = new NpgsqlConnection(database.ConnectionString); await changer.OpenAsync();
        await using var writeTransaction = await writer.BeginTransactionAsync(isolation);
        await using var snapshot = new NpgsqlCommand("SELECT \"Title\" FROM \"Task\" WHERE \"TaskId\"=@id", writer, writeTransaction);
        snapshot.Parameters.AddWithValue("id", a.Task); Assert.NotNull(await snapshot.ExecuteScalarAsync());
        await using var changeTransaction = await changer.BeginTransactionAsync();
        await using var update = new NpgsqlCommand(department
            ? "UPDATE \"Department\" SET \"Status\"='INACTIVE' WHERE \"DepartmentId\"=@id"
            : "UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"=@id", changer, changeTransaction);
        update.Parameters.AddWithValue("id", department ? a.Department : a.User); await update.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", writer, writeTransaction); var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "TaskAssignment" ("TaskAssignmentId","TaskId","AssignedBy","DepartmentId","UserId","AssignedAt")
            VALUES (@id,@task,@actor,@department,@user,now())
            """, writer, writeTransaction);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7()); insert.Parameters.AddWithValue("task", a.Task); insert.Parameters.AddWithValue("actor", a.User);
        insert.Parameters.AddWithValue("department", NpgsqlTypes.NpgsqlDbType.Uuid, department ? a.Department : DBNull.Value);
        insert.Parameters.AddWithValue("user", NpgsqlTypes.NpgsqlDbType.Uuid, department ? DBNull.Value : a.User);
        var pending = insert.ExecuteNonQueryAsync(); var blocked = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE pid={pid} AND wait_event_type='Lock'").SingleAsync() > 0;
            if (blocked) break;
            await Task.Delay(25);
        }
        await changeTransaction.CommitAsync(); Assert.Equal(error, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.True(blocked); Assert.Empty(await db.TaskAssignments.ToListAsync());
    }

    [Fact]
    public async Task Empty_rollback_reapplies_but_existing_assignment_history_is_preserved()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261004133416_TaskEvidenceHistory");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await SeedAsync(fixture); await using var db = fixture.Create(a.User, a.Tenant); db.Add(New(a)); await db.SaveChangesAsync();
            await Check(() => migration.GetService<IMigrator>().MigrateAsync("20261004133416_TaskEvidenceHistory"));
            Assert.Contains("20261004134916_TaskAssignmentHistory", await migration.Database.GetAppliedMigrationsAsync());
            Assert.Single(await db.TaskAssignments.ToListAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Approved_department_acceptance_claim_requires_live_membership_and_preserves_original_metadata()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var member = await AddMemberAsync(db, a); var assignment = TaskAssignment.Create(a.Task, a.User, a.Department, null, DateTimeOffset.UtcNow);
        db.Add(assignment); await db.SaveChangesAsync(); await db.Entry(assignment).ReloadAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"={a.Task}");
        var parent = await db.WorkTasks.AsNoTracking().SingleAsync(); var nonmember = await db.Users.AsNoTracking().SingleAsync(u => u.Id == a.User);
        Assert.Throws<InvalidOperationException>(() => assignment.Accept(parent, nonmember, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.Accept(parent, member, assignment.AssignedAt.AddSeconds(-1)));
        Assert.Null(assignment.UserId); Assert.Null(assignment.AcceptedAt);
        var assignedAt = assignment.AssignedAt; assignment.Accept(parent, member, DateTimeOffset.UtcNow);
        Assert.Equal(member.Id, assignment.UserId); Assert.NotNull(assignment.AcceptedAt);
        Assert.Equal(a.Department, assignment.DepartmentId); Assert.Equal(a.User, assignment.AssignedBy); Assert.Equal(assignedAt, assignment.AssignedAt);
        Assert.Equal(TaskState.Assigned, parent.Status); Assert.Throws<InvalidOperationException>(() => assignment.Accept(parent, member, DateTimeOffset.UtcNow));
        // Domain acceptance cannot bypass the still-unimplemented Application transaction via EF.
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"AcceptedAt\"=now() WHERE \"TaskAssignmentId\"={assignment.Id}"));
        foreach (var forbidden in new[] { a.User, b.User })
            await Check(() => ClaimAsync(db, assignment.Id, forbidden));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='INACTIVE' WHERE \"UserId\"={member.Id}");
        await Check(() => ClaimAsync(db, assignment.Id, member.Id));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\"='ACTIVE' WHERE \"UserId\"={member.Id}");
        await ClaimAsync(db, assignment.Id, member.Id);
        var claimed = await db.TaskAssignments.AsNoTracking().SingleAsync();
        Assert.Equal(member.Id, claimed.UserId); Assert.Equal(assignedAt, claimed.AssignedAt); Assert.Equal(a.User, claimed.AssignedBy);
        Assert.Equal(a.Department, claimed.DepartmentId); Assert.NotNull(claimed.AcceptedAt);
        await Check(() => ClaimAsync(db, assignment.Id, member.Id));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"UserId\"=NULL WHERE \"TaskAssignmentId\"={assignment.Id}"));
    }

    [Fact]
    public async Task Concurrent_department_claims_have_one_immutable_accepting_user()
    {
        // Every race must have a winner; repetition does not retry or suppress a failed assertion.
        for (var iteration = 0; iteration < 20; iteration++) await VerifyConcurrentDepartmentClaimAsync();
    }

    private async Task VerifyConcurrentDepartmentClaimAsync()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var first = await AddMemberAsync(db, a); var second = await AddMemberAsync(db, a);
        var assignment = TaskAssignment.Create(a.Task, a.User, a.Department, null, DateTimeOffset.UtcNow); db.Add(assignment); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"={a.Task}");
        async Task<(Guid? Member, string? Error)> Claim(Guid member)
        {
            await using var claimant = database.Create(member, a.Tenant);
            try { await ClaimAsync(claimant, assignment.Id, member); return (member, null); }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.CheckViolation) { return (null, exception.MessageText); }
        }
        var results = await Task.WhenAll(Claim(first.Id), Claim(second.Id));
        Assert.True(results.Any(result => result.Member is not null), string.Join("; ", results.Select(result => result.Error)));
        var winner = Assert.Single(results, result => result.Member is not null);
        var saved = await db.TaskAssignments.AsNoTracking().SingleAsync(); Assert.Equal(winner.Member, saved.UserId); Assert.NotNull(saved.AcceptedAt);
    }

    private static async Task<UserAccount> AddMemberAsync(BizFlowDbContext db, Seed a)
    {
        var code = Guid.NewGuid().ToString("N");
        var user = UserAccount.CreateTenantUser(a.Tenant, code, code + "@example.test", "Queue member", "test-only-hash", DateTimeOffset.UtcNow, a.Department);
        db.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static Task<int> ClaimAsync(BizFlowDbContext db, Guid assignment, Guid member) =>
        // Use the same application clock as AssignedAt, as the future use case will. SQL now()
        // uses the container clock and must not turn this concurrency test into a clock-skew test.
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"UserId\"={member},\"AcceptedAt\"={DateTimeOffset.UtcNow} WHERE \"TaskAssignmentId\"={assignment}");
    private sealed record Seed(Guid Tenant, Guid User, Guid Department, Guid Task);
    private static TaskAssignment New(Seed a) => TaskAssignment.Create(a.Task, a.User, a.Department, a.User, DateTimeOffset.UtcNow);
    private static async Task<Seed> SeedAsync(PostgresFixture fixture)
    {
        var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
        var department = Department.Create(a.TenantId, "OPS", "Operations", null, DateTimeOffset.UtcNow);
        var task = WorkTask.CreateDraft(a.TenantId, a.UserId, "Task", DateTimeOffset.UtcNow); db.AddRange(department, task); await db.SaveChangesAsync();
        return new(a.TenantId, a.UserId, department.Id, task.Id);
    }
    private static Task<int> InsertAsync(BizFlowDbContext db, Guid task, Guid actor, Guid? department, Guid? user) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TaskAssignment" ("TaskAssignmentId","TaskId","AssignedBy","DepartmentId","UserId","AssignedAt")
            VALUES ({Guid.CreateVersion7()},{task},{actor},{department},{user},now())
            """);
    private static async Task Check(Func<Task> action) => Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);
}
