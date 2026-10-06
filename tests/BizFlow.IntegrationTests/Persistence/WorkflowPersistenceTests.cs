using BizFlow.Application.Common;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class WorkflowPersistenceTests(PostgresFixture fixture)
{
    internal static async Task<(Guid Workflow, Guid Version, Guid Step, Guid Transition)> SeedAsync(BizFlowDbContext db, Guid tenant)
    {
        var root = WorkflowDefinition.CreateDraft(tenant, "Test workflow", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        var version = WorkflowVersion.CreateDraft(root.Id, 1);
        var step = WorkflowStep.CreateDraft(version.Id, "WORK", "Work", WorkflowStepType.Action, 1);
        var transition = WorkflowTransition.CreateDraft(version.Id, "DRAFT", "ASSIGNED");
        db.AddRange(root, version, step, transition);
        await db.SaveChangesAsync();
        return (root.Id, version.Id, step.Id, transition.Id);
    }

    [Fact]
    public async Task Entire_graph_is_tenant_filtered_and_foreign_parent_writes_fail_closed()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var dbA = fixture.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(dbA, a.TenantId);
        Assert.Single(await dbA.Workflows.ToListAsync()); Assert.Single(await dbA.WorkflowVersions.ToListAsync());
        Assert.Single(await dbA.WorkflowSteps.ToListAsync()); Assert.Single(await dbA.WorkflowTransitions.ToListAsync());
        foreach (var context in new[] { fixture.Create(b.UserId, b.TenantId), fixture.Create(null, a.TenantId), fixture.Create(fixture.PlatformUserId, null) })
        {
            await using var db = context;
            Assert.Empty(await db.Workflows.ToListAsync()); Assert.Empty(await db.WorkflowVersions.ToListAsync());
            Assert.Empty(await db.WorkflowSteps.ToListAsync()); Assert.Empty(await db.WorkflowTransitions.ToListAsync());
            db.WorkflowVersions.Add(WorkflowVersion.CreateDraft(graph.Workflow, 2));
            Assert.Equal("PERSISTENCE.SCOPE_DENIED", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
            db.ChangeTracker.Clear();
            db.WorkflowSteps.Add(WorkflowStep.CreateDraft(graph.Version, "FOREIGN", "Foreign", WorkflowStepType.Action, 2));
            Assert.Equal("PERSISTENCE.SCOPE_DENIED", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
        }
    }

    [Fact]
    public async Task Attached_forged_parent_and_detached_foreign_child_cannot_change_other_tenant()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var dbA = fixture.Create(a.UserId, a.TenantId);
        await using var dbB = fixture.Create(b.UserId, b.TenantId);
        var graphA = await SeedAsync(dbA, a.TenantId); var graphB = await SeedAsync(dbB, b.TenantId);
        var forged = WorkflowDefinition.CreateDraft(a.TenantId, "Forged", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        dbA.Entry(forged).Property(x => x.Id).CurrentValue = graphB.Workflow;
        dbA.Attach(forged);
        dbA.WorkflowVersions.Add(WorkflowVersion.CreateDraft(graphB.Workflow, 2));
        Assert.Equal("PERSISTENCE.SCOPE_DENIED", (await Assert.ThrowsAsync<ApplicationFault>(() => dbA.SaveChangesAsync())).Code);
        dbA.ChangeTracker.Clear();
        var child = WorkflowStep.CreateDraft(graphA.Version, "WORK", "Work", WorkflowStepType.Action, 1);
        dbA.Entry(child).Property(x => x.Id).CurrentValue = graphB.Step;
        dbA.Attach(child);
        dbA.Entry(child).Property<uint>("Version").OriginalValue = dbB.Entry(await dbB.WorkflowSteps.SingleAsync()).Property<uint>("Version").CurrentValue;
        dbA.Entry(child).Property(x => x.Name).CurrentValue = "Forged edit";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbA.SaveChangesAsync());
        Assert.Equal("Work", (await dbB.WorkflowSteps.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task Published_version_and_both_child_types_are_immutable_even_for_stale_tracking_and_direct_SQL()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        // Test-only publication fixture; this is not an application publishing use case.
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "WorkflowVersion" SET "Status" = 'PUBLISHED', "PublishedAt" = now() WHERE "WorkflowVersionId" = {graph.Version}""");
        db.Entry(await db.WorkflowSteps.SingleAsync()).Property(x => x.Name).CurrentValue = "Stale edit";
        Assert.Equal("WORKFLOW.VERSION_IMMUTABLE", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
        db.ChangeTracker.Clear();
        db.WorkflowTransitions.Add(WorkflowTransition.CreateDraft(graph.Version, "ASSIGNED", "ACCEPTED"));
        Assert.Equal("WORKFLOW.VERSION_IMMUTABLE", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
        db.ChangeTracker.Clear();
        db.Entry(await db.WorkflowVersions.SingleAsync()).Property(x => x.DefinitionJson).CurrentValue = "{\"changed\":true}";
        Assert.Equal("WORKFLOW.VERSION_IMMUTABLE", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
        FormattableString[] writes = [
            $"""UPDATE "WorkflowVersion" SET "DefinitionJson" = jsonb_build_object() WHERE "WorkflowVersionId" = {graph.Version}""",
            $"""DELETE FROM "WorkflowVersion" WHERE "WorkflowVersionId" = {graph.Version}""",
            $"""UPDATE "WorkflowStep" SET "Name" = 'Changed' WHERE "WorkflowStepId" = {graph.Step}""",
            $"""DELETE FROM "WorkflowStep" WHERE "WorkflowStepId" = {graph.Step}""",
            $"""UPDATE "WorkflowTransition" SET "ToState" = 'ACCEPTED' WHERE "TransitionId" = {graph.Transition}""",
            $"""DELETE FROM "WorkflowTransition" WHERE "TransitionId" = {graph.Transition}""",
            $"""INSERT INTO "WorkflowStep" ("WorkflowStepId", "WorkflowVersionId", "StepCode", "Name", "Type", "OrderNo", "ConfigJson") VALUES ({Guid.NewGuid()}, {graph.Version}, 'NEW', 'New', 'ACTION', 2, jsonb_build_object())""",
            $"""INSERT INTO "WorkflowTransition" ("TransitionId", "WorkflowVersionId", "FromState", "ToState") VALUES ({Guid.NewGuid()}, {graph.Version}, 'ASSIGNED', 'ACCEPTED')"""
        ];
        foreach (var write in writes)
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(write))).SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE \"Workflow\" CASCADE"))).SqlState);
        Assert.Equal(WorkflowVersionStatus.Published, (await db.WorkflowVersions.AsNoTracking().SingleAsync()).Status);
        Assert.Single(await db.WorkflowSteps.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Draft_constraints_and_immutable_ownership_are_enforced_in_SQL()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        var other = await SeedAsync(db, a.TenantId);
        FormattableString[] invalid = [
            $"""UPDATE "Workflow" SET "TenantId" = {b.TenantId} WHERE "WorkflowId" = {graph.Workflow}""",
            $"""UPDATE "Workflow" SET "BusinessType" = 'REQUEST' WHERE "WorkflowId" = {graph.Workflow}""",
            $"""UPDATE "WorkflowVersion" SET "WorkflowId" = {other.Workflow} WHERE "WorkflowVersionId" = {graph.Version}""",
            $"""UPDATE "WorkflowVersion" SET "DefinitionJson" = '[]' WHERE "WorkflowVersionId" = {graph.Version}""",
            $"""UPDATE "WorkflowVersion" SET "Status" = 'PUBLISHED' WHERE "WorkflowVersionId" = {graph.Version}""",
            $"""UPDATE "WorkflowStep" SET "WorkflowVersionId" = {other.Version} WHERE "WorkflowStepId" = {graph.Step}""",
            $"""UPDATE "WorkflowStep" SET "OrderNo" = 0 WHERE "WorkflowStepId" = {graph.Step}""",
            $"""UPDATE "WorkflowStep" SET "StepCode" = 'invalid space' WHERE "WorkflowStepId" = {graph.Step}""",
            $"""UPDATE "WorkflowStep" SET "ConfigJson" = 'false' WHERE "WorkflowStepId" = {graph.Step}""",
            $"""UPDATE "WorkflowTransition" SET "GuardJson" = '[]' WHERE "TransitionId" = {graph.Transition}""",
            $"""UPDATE "WorkflowTransition" SET "WorkflowVersionId" = {other.Version} WHERE "TransitionId" = {graph.Transition}"""
        ];
        foreach (var write in invalid)
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(write))).SqlState);
        db.WorkflowVersions.Add(WorkflowVersion.CreateDraft(graph.Workflow, 1));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync())).InnerException).SqlState);
        db.ChangeTracker.Clear();
        db.WorkflowSteps.Add(WorkflowStep.CreateDraft(graph.Version, "WORK", "Duplicate code", WorkflowStepType.Action, 2));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync())).InnerException).SqlState);
        db.ChangeTracker.Clear();
        db.WorkflowSteps.Add(WorkflowStep.CreateDraft(graph.Version, "OTHER", "Duplicate order", WorkflowStepType.Action, 1));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync())).InnerException).SqlState);
        db.ChangeTracker.Clear();
        db.Entry(await db.WorkflowSteps.SingleAsync(s => s.Id == graph.Step)).Property(x => x.Name).CurrentValue = "Draft edit";
        await db.SaveChangesAsync();
        Assert.Equal("Draft edit", (await db.WorkflowSteps.AsNoTracking().SingleAsync(s => s.Id == graph.Step)).Name);
    }

    [Fact]
    public async Task Child_writer_waiting_on_publication_rechecks_committed_status()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var graph = await SeedAsync(db, a.TenantId);
        await using var publisher = new NpgsqlConnection(fixture.ConnectionString);
        await using var writer = new NpgsqlConnection(fixture.ConnectionString);
        await publisher.OpenAsync(); await writer.OpenAsync();
        await using var transaction = await publisher.BeginTransactionAsync();
        await using var publish = new NpgsqlCommand("UPDATE \"WorkflowVersion\" SET \"Status\"='PUBLISHED', \"PublishedAt\"=now() WHERE \"WorkflowVersionId\"=@id", publisher, transaction);
        publish.Parameters.AddWithValue("id", graph.Version); await publish.ExecuteNonQueryAsync();
        await using var pidQuery = new NpgsqlCommand("SELECT pg_backend_pid()", writer);
        var pid = (int)(await pidQuery.ExecuteScalarAsync())!;
        await using var insert = new NpgsqlCommand("INSERT INTO \"WorkflowTransition\" (\"TransitionId\", \"WorkflowVersionId\", \"FromState\", \"ToState\") VALUES (@id, @version, 'ASSIGNED', 'ACCEPTED')", writer);
        insert.Parameters.AddWithValue("id", Guid.NewGuid()); insert.Parameters.AddWithValue("version", graph.Version);
        var pending = insert.ExecuteNonQueryAsync();
        var blocked = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE pid = {pid} AND wait_event_type = 'Lock'").SingleAsync() > 0;
            if (blocked) break;
            await Task.Delay(25);
        }
        await transaction.CommitAsync();
        Assert.True(blocked, "The child writer must serialize on the publication transaction.");
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => pending)).SqlState);
        Assert.Single(await db.WorkflowTransitions.AsNoTracking().ToListAsync());
    }
}
