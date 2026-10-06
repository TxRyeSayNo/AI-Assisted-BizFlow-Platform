using System.Data;
using BizFlow.Application.Common;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TaskDraftPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Independent_drafts_checklists_priorities_and_nullable_fields_round_trip()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(7));
        foreach (var priority in Enum.GetValues<TaskPriority>())
        {
            var task = WorkTask.CreateDraft(a.TenantId, a.UserId, priority.ToString(), now, "<b>Literal</b>\nDetails", priority, now.AddDays(1));
            db.AddRange(task, TaskChecklistItem.Create(task.Id, "Verify", 1), TaskChecklistItem.Create(task.Id, "Document", 2));
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(8, await db.TaskChecklistItems.CountAsync());
        foreach (var task in await db.WorkTasks.ToListAsync())
        {
            Assert.Equal(Enum.Parse<TaskPriority>(task.Title), task.Priority); Assert.Equal(TaskState.Draft, task.Status);
            Assert.Equal(now.ToUniversalTime(), task.CreatedAt); Assert.Equal(task.CreatedAt, task.UpdatedAt);
            Assert.Equal(now.AddDays(1).ToUniversalTime(), task.Deadline); Assert.Equal("<b>Literal</b>\nDetails", task.Description);
            Assert.Null(task.RequestId); Assert.Null(task.WorkflowVersionId); Assert.Null(task.SlaVersionId); Assert.Null(task.CompletedAt); Assert.Null(task.DeletedAt);
            Assert.All(await db.TaskChecklistItems.Where(i => i.TaskId == task.Id).ToListAsync(), i => { Assert.False(i.IsCompleted); Assert.Null(i.CompletedBy); Assert.Null(i.CompletedAt); });
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Task" ("TaskId","TenantId","CreatorId","Title","CreatedAt","UpdatedAt")
            VALUES ({Guid.CreateVersion7()},{a.TenantId},{a.UserId},'Default',now(),now())
            """);
        var defaults = await db.WorkTasks.SingleAsync(t => t.Title == "Default");
        Assert.Equal(TaskPriority.Medium, defaults.Priority); Assert.Null(defaults.Description); Assert.Null(defaults.Deadline);
    }

    [Fact]
    public async Task Request_can_have_zero_or_multiple_independent_tasks_without_request_state_or_version_changes()
    {
        var a = await SeedRequestAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var original = await db.Requests.SingleAsync(); var version = db.Entry(original).Property<uint>("Version").CurrentValue;
        Assert.Empty(await db.WorkTasks.Where(t => t.RequestId == original.Id).ToListAsync());
        var first = WorkTask.CreateDraft(a.Tenant, a.User, "First", DateTimeOffset.UtcNow, requestId: a.Request);
        var second = WorkTask.CreateDraft(a.Tenant, a.User, "Second", DateTimeOffset.UtcNow, requestId: a.Request);
        db.AddRange(first, second); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(2, await db.WorkTasks.CountAsync(t => t.RequestId == a.Request));
        Assert.NotEqual(first.Id, second.Id); var request = await db.Requests.SingleAsync();
        Assert.Equal(RequestState.Draft, request.Status); Assert.Equal(original.UpdatedAt, request.UpdatedAt);
        Assert.Equal(version, db.Entry(request).Property<uint>("Version").CurrentValue);
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"RequestId\"=NULL WHERE \"TaskId\"={first.Id}"));
    }

    [Fact]
    public async Task Tenant_filters_persisted_reference_guards_and_existing_row_mutation_denials_fail_closed()
    {
        var a = await SeedRequestAsync(database); var b = await SeedRequestAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        var own = Draft(a); var other = Draft(b); db.Add(own); foreign.Add(other); await db.SaveChangesAsync(); await foreign.SaveChangesAsync();
        db.Add(TaskChecklistItem.Create(own.Id, "Own", 1)); foreign.Add(TaskChecklistItem.Create(other.Id, "Other", 1));
        await db.SaveChangesAsync(); await foreign.SaveChangesAsync();
        Assert.Single(await db.WorkTasks.ToListAsync()); Assert.Single(await db.TaskChecklistItems.ToListAsync());
        await using var anonymous = database.Create(null, a.Tenant); await using var platform = database.Create(database.PlatformUserId, null);
        foreach (var context in new[] { anonymous, platform })
        {
            Assert.Empty(await context.WorkTasks.ToListAsync()); Assert.Empty(await context.TaskChecklistItems.ToListAsync());
            context.Add(Draft(a)); await Assert.ThrowsAsync<ApplicationFault>(() => context.SaveChangesAsync());
        }
        foreach (var invalid in new[] { Draft(b), Draft(a with { User = b.User }), Draft(a with { Request = b.Request }) })
        {
            db.Add(invalid); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        var forged = Draft(a); db.Entry(forged).Property(t => t.Id).CurrentValue = other.Id; db.Attach(forged);
        db.Add(TaskChecklistItem.Create(other.Id, "Forged owner", 1)); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var forgedRequest = await foreign.Requests.AsNoTracking().SingleAsync(); db.Entry(forgedRequest).Property(r => r.TenantId).CurrentValue = a.Tenant;
        db.Attach(forgedRequest); db.Add(Draft(a with { Request = b.Request })); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Attach(own); db.Entry(own).Property(t => t.Status).CurrentValue = TaskState.Assigned;
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Remove(own); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"DeletedAt\"=now() WHERE \"TaskId\"={own.Id}");
        Assert.Empty(await db.WorkTasks.ToListAsync()); Assert.Empty(await db.TaskChecklistItems.ToListAsync());
        Assert.Equal(1, await db.WorkTasks.IgnoreQueryFilters().CountAsync(t => t.Id == own.Id));
    }

    [Fact]
    public async Task Database_validates_ownership_published_task_workflow_and_immutable_version_references()
    {
        var a = await SeedRequestAsync(database); var b = await SeedRequestAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        var task = Draft(a); db.Add(task); await db.SaveChangesAsync();
        await Check(() => InsertTaskAsync(db, a with { User = b.User })); await Check(() => InsertTaskAsync(db, a with { Request = b.Request }));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"CreatorId\"={b.User} WHERE \"TaskId\"={task.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"TenantId\"={b.Tenant} WHERE \"TaskId\"={task.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Title\"={"Bad\ntext"} WHERE \"TaskId\"={task.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Description\"={"Bad\u0001text"} WHERE \"TaskId\"={task.Id}"));
        var workflow = WorkflowDefinition.CreateDraft(a.Tenant, "Task flow", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        var requestFlow = WorkflowDefinition.CreateDraft(a.Tenant, "Request flow", WorkflowBusinessType.Request, DateTimeOffset.UtcNow);
        var taskVersion = WorkflowVersion.CreateDraft(workflow.Id, 1); var requestVersion = WorkflowVersion.CreateDraft(requestFlow.Id, 1);
        db.AddRange(workflow, requestFlow, taskVersion, requestVersion); await db.SaveChangesAsync();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"WorkflowVersionId\"={taskVersion.Id} WHERE \"TaskId\"={task.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowVersion\" SET \"Status\"='PUBLISHED',\"PublishedAt\"=now() WHERE \"WorkflowVersionId\" IN ({taskVersion.Id},{requestVersion.Id})");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"WorkflowVersionId\"={requestVersion.Id} WHERE \"TaskId\"={task.Id}"));
        var foreignFlow = WorkflowDefinition.CreateDraft(b.Tenant, "Foreign", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        var foreignVersion = WorkflowVersion.CreateDraft(foreignFlow.Id, 1); foreign.AddRange(foreignFlow, foreignVersion); await foreign.SaveChangesAsync();
        await foreign.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowVersion\" SET \"Status\"='PUBLISHED',\"PublishedAt\"=now() WHERE \"WorkflowVersionId\"={foreignVersion.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"WorkflowVersionId\"={foreignVersion.Id} WHERE \"TaskId\"={task.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"WorkflowVersionId\"={taskVersion.Id} WHERE \"TaskId\"={task.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"WorkflowVersionId\"=NULL WHERE \"TaskId\"={task.Id}"));
        var ownSla = await SeedSlaAsync(db, a.Tenant); var otherSla = await SeedSlaAsync(foreign, b.Tenant);
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"SLAVersionId\"={otherSla} WHERE \"TaskId\"={task.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"SLAVersionId\"={ownSla} WHERE \"TaskId\"={task.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"SLAVersionId\"=NULL WHERE \"TaskId\"={task.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Task\" WHERE \"TaskId\"={task.Id}"));
        await Check(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Task\" CASCADE"));
    }

    [Fact]
    public async Task Checklist_completion_consistency_tenant_ownership_and_parent_identity_are_protected()
    {
        var a = await SeedRequestAsync(database); var b = await SeedRequestAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var task = Draft(a); var second = Draft(a); var item = TaskChecklistItem.Create(task.Id, "Verify", 1);
        db.AddRange(task, second, item); await db.SaveChangesAsync();
        db.Entry(item).Property(i => i.Title).CurrentValue = "Unimplemented edit";
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"TaskId\"={second.Id} WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"IsCompleted\"=TRUE WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"CompletedBy\"={a.User} WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"SortOrder\"=0 WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"Title\"={"Bad\ntext"} WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"IsCompleted\"=TRUE,\"CompletedBy\"={b.User},\"CompletedAt\"=now() WHERE \"ChecklistItemId\"={item.Id}"));
        // Test-only historical fixture, not an exposed completion command.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskChecklistItem\" SET \"IsCompleted\"=TRUE,\"CompletedBy\"={a.User},\"CompletedAt\"=now() WHERE \"ChecklistItemId\"={item.Id}");
        Assert.True((await db.TaskChecklistItems.SingleAsync()).IsCompleted);
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"TaskChecklistItem\" WHERE \"ChecklistItemId\"={item.Id}"));
        await Check(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"TaskChecklistItem\""));
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, PostgresErrorCodes.CheckViolation)]
    [InlineData(IsolationLevel.RepeatableRead, PostgresErrorCodes.SerializationFailure)]
    [InlineData(IsolationLevel.Serializable, PostgresErrorCodes.SerializationFailure)]
    public async Task Initial_checklist_insert_cannot_use_stale_draft_state(IsolationLevel isolation, string error)
    {
        var a = await SeedRequestAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var task = Draft(a); db.Add(task); await db.SaveChangesAsync();
        await using var writer = new NpgsqlConnection(database.ConnectionString); await writer.OpenAsync();
        await using var changer = new NpgsqlConnection(database.ConnectionString); await changer.OpenAsync();
        await using var writeTransaction = await writer.BeginTransactionAsync(isolation);
        await using var snapshot = new NpgsqlCommand("SELECT \"Title\" FROM \"Task\" WHERE \"TaskId\"=@id", writer, writeTransaction);
        snapshot.Parameters.AddWithValue("id", task.Id); Assert.NotNull(await snapshot.ExecuteScalarAsync());
        await using var changeTransaction = await changer.BeginTransactionAsync();
        await using var update = new NpgsqlCommand("UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"=@id", changer, changeTransaction);
        update.Parameters.AddWithValue("id", task.Id); await update.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", writer, writeTransaction);
        var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "TaskChecklistItem" ("ChecklistItemId","TaskId","Title") VALUES (@id,@task,'Stale checklist')
            """, writer, writeTransaction);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7()); insert.Parameters.AddWithValue("task", task.Id);
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
        Assert.True(blocked); Assert.Empty(await db.TaskChecklistItems.ToListAsync());
    }

    [Fact]
    public async Task Empty_rollback_reapplies_and_nonempty_task_history_blocks_rollback()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261004075754_RequestResolutionHistory");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await SeedRequestAsync(fixture); await using var db = fixture.Create(a.User, a.Tenant);
            var task = Draft(a); db.AddRange(task, TaskChecklistItem.Create(task.Id, "Retain", 1)); await db.SaveChangesAsync();
            await Check(() => migration.GetService<IMigrator>().MigrateAsync("20261004075754_RequestResolutionHistory"));
            Assert.Contains("20261004081325_TaskDraftFoundation", await migration.Database.GetAppliedMigrationsAsync());
            Assert.Single(await db.WorkTasks.ToListAsync()); Assert.Single(await db.TaskChecklistItems.ToListAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }

    private sealed record Seed(Guid Tenant, Guid User, Guid Request);
    private static WorkTask Draft(Seed a) => WorkTask.CreateDraft(a.Tenant, a.User, "Task", DateTimeOffset.UtcNow, requestId: a.Request);
    private static async Task<Seed> SeedRequestAsync(PostgresFixture fixture)
    {
        var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
        var service = InternalService.Create(a.TenantId, "IT", "Support", null, true, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, "GENERAL", "General"); db.AddRange(service, category); await db.SaveChangesAsync();
        var request = WorkRequest.CreateDraft(a.TenantId, a.UserId, service.Id, category.Id, "Request", "Details", DateTimeOffset.UtcNow);
        db.Add(request); await db.SaveChangesAsync(); return new(a.TenantId, a.UserId, request.Id);
    }
    private static Task<int> InsertTaskAsync(BizFlowDbContext db, Seed a) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "Task" ("TaskId","TenantId","CreatorId","RequestId","Title","CreatedAt","UpdatedAt")
        VALUES ({Guid.CreateVersion7()},{a.Tenant},{a.User},{a.Request},'Task',now(),now())
        """);
    private static async Task<Guid> SeedSlaAsync(BizFlowDbContext db, Guid tenant)
    {
        var profile = SlaProfile.CreateDraft(tenant, "SLA");
        var calendar = BusinessCalendar.Create(tenant, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var version = SlaVersion.CreateSnapshot(profile.Id, 1, 60, 45, calendar.Id);
        db.AddRange(profile, calendar, version); await db.SaveChangesAsync(); return version.Id;
    }
    private static async Task Check(Func<Task> action) => Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);
}
