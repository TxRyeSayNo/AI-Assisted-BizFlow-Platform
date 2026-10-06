using BizFlow.Application.Common;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RequestDraftPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Every_explicit_priority_round_trips_including_zero_valued_low_and_database_default_is_medium()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        foreach (var priority in Enum.GetValues<RequestPriority>())
            db.Requests.Add(WorkRequest.CreateDraft(a.Tenant, a.User, a.Service, a.Category, priority.ToString(), "Details", DateTimeOffset.UtcNow, priority));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var request in await db.Requests.ToListAsync()) Assert.Equal(Enum.Parse<RequestPriority>(request.Title), request.Priority);
        await InsertDraftAsync(db, a);
        Assert.Equal(RequestPriority.Medium, (await db.Requests.SingleAsync(r => r.Title == "Title")).Priority);
    }
    [Fact]
    public async Task Drafts_preserve_fields_and_fail_closed_across_tenant_and_reference_boundaries()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var own = database.Create(a.User, a.Tenant); await using var other = database.Create(b.User, b.Tenant);
        var request = Draft(a); own.Requests.Add(request); await own.SaveChangesAsync();
        other.Requests.Add(Draft(b)); await other.SaveChangesAsync();
        var saved = await own.Requests.AsNoTracking().SingleAsync();
        Assert.Equal(request.Id, saved.Id); Assert.Equal(RequestState.Draft, saved.Status); Assert.Equal(RequestPriority.Medium, saved.Priority);
        Assert.Equal(a.Category, saved.CategoryId); Assert.Equal("<script>literal</script>\nDetails", saved.Description);
        Assert.Single(await other.Requests.ToListAsync());
        await using var anonymous = database.Create(null, a.Tenant); await using var platform = database.Create(database.PlatformUserId, null);
        Assert.Empty(await anonymous.Requests.ToListAsync()); Assert.Empty(await platform.Requests.ToListAsync());
        foreach (var invalid in new[] {
            Draft(b), Draft(a with { User = b.User }), Draft(a with { Service = b.Service, Category = b.Category }),
            Draft(a with { Category = b.Category }), Draft(a, parent: (await other.Requests.SingleAsync()).Id)
        })
        {
            own.Requests.Add(invalid); await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        }
        // Detached foreign parent impersonation must not override persisted ownership.
        var foreign = await other.Requests.AsNoTracking().SingleAsync(); var forged = Draft(a);
        own.Entry(forged).Property(r => r.Id).CurrentValue = foreign.Id; own.Attach(forged);
        own.Requests.Add(Draft(a, parent: foreign.Id));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        own.Attach(request); own.Entry(request).Property(r => r.Title).CurrentValue = "Not an implemented edit";
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        own.Requests.Remove(request); await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync());
    }

    [Fact]
    public async Task Revision_is_new_draft_and_rejected_history_cannot_be_reopened_modified_or_deleted()
    {
        var a = await SeedAsync(database); await using var db = database.Create(a.User, a.Tenant);
        var original = Draft(a); db.Requests.Add(original); await db.SaveChangesAsync();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"RevisedFromRequestId\"={original.Id} WHERE \"RequestId\"={original.Id}"));
        // Test-only historical fixture; no runtime transition API is implemented by this slice.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='REJECTED' WHERE \"RequestId\"={original.Id}");
        db.ChangeTracker.Clear(); original = await db.Requests.SingleAsync();
        var originalVersion = db.Entry(original).Property<uint>("Version").CurrentValue;
        var revision = WorkRequest.ReviseRejected(original, a.User, a.Service, a.Category, "Corrected", "New information", DateTimeOffset.UtcNow);
        db.Requests.Add(revision); await db.SaveChangesAsync();
        Assert.Equal(original.Id, revision.RevisedFromRequestId); Assert.NotEqual(original.Id, revision.Id);
        Assert.Equal(RequestState.Draft, revision.Status); Assert.Null(revision.WorkflowVersionId);
        foreach (var statement in new[] {
            "UPDATE \"Request\" SET \"Title\"='Changed' WHERE \"RequestId\"={0}",
            "UPDATE \"Request\" SET \"Status\"='DRAFT' WHERE \"RequestId\"={0}",
            "UPDATE \"Request\" SET \"DeletedAt\"=now() WHERE \"RequestId\"={0}",
            "UPDATE \"Request\" SET \"UpdatedAt\"=now() WHERE \"RequestId\"={0}",
            "UPDATE \"Request\" SET \"Description\"=\"Description\" WHERE \"RequestId\"={0}" })
            await Check(() => db.Database.ExecuteSqlRawAsync(statement, original.Id));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Request\" WHERE \"RequestId\"={original.Id}"));
        db.ChangeTracker.Clear(); var source = await db.Requests.SingleAsync(r => r.Id == original.Id);
        Assert.Equal(RequestState.Rejected, source.Status); Assert.Equal(original.Title, source.Title);
        Assert.Equal(original.UpdatedAt, source.UpdatedAt); Assert.Null(source.DeletedAt);
        Assert.Equal(originalVersion, db.Entry(source).Property<uint>("Version").CurrentValue);
        var fakeSource = Draft(a); db.Entry(fakeSource).Property(r => r.Id).CurrentValue = revision.Id;
        db.Entry(fakeSource).Property(r => r.Status).CurrentValue = RequestState.Rejected;
        db.Requests.Add(WorkRequest.ReviseRejected(fakeSource, a.User, a.Service, a.Category, "Invalid", "Details", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_guards_validate_references_parent_cycles_plain_text_and_pinned_versions()
    {
        var a = await SeedAsync(database); var b = await SeedAsync(database);
        await using var db = database.Create(a.User, a.Tenant); await using var foreign = database.Create(b.User, b.Tenant);
        var parent = Draft(a); db.Requests.Add(parent); await db.SaveChangesAsync();
        var child = Draft(a, parent: parent.Id); db.Requests.Add(child); await db.SaveChangesAsync();
        var otherRequest = Draft(b); foreign.Requests.Add(otherRequest); await foreign.SaveChangesAsync();
        foreach (var invalid in new[] { a with { User = b.User }, a with { Service = b.Service }, a with { Category = b.Category } })
            await Check(() => InsertDraftAsync(db, invalid));
        await Check(() => InsertDraftAsync(db, a, parent: otherRequest.Id));
        await Check(() => InsertDraftAsync(db, a, revision: parent.Id));
        await foreign.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Status\"='REJECTED' WHERE \"RequestId\"={otherRequest.Id}");
        await Check(() => InsertDraftAsync(db, a, revision: otherRequest.Id));
        foreach (var (statement, value) in new[] {
            ("UPDATE \"Request\" SET \"TenantId\"={0} WHERE \"RequestId\"={1}", b.Tenant),
            ("UPDATE \"Request\" SET \"RequesterId\"={0} WHERE \"RequestId\"={1}", b.User),
            ("UPDATE \"Request\" SET \"ServiceId\"={0} WHERE \"RequestId\"={1}", b.Service),
            ("UPDATE \"Request\" SET \"CategoryId\"={0} WHERE \"RequestId\"={1}", b.Category),
            ("UPDATE \"Request\" SET \"ParentRequestId\"={0} WHERE \"RequestId\"={1}", child.Id),
            ("UPDATE \"Request\" SET \"RevisedFromRequestId\"={0} WHERE \"RequestId\"={1}", otherRequest.Id) })
            await Check(() => db.Database.ExecuteSqlRawAsync(statement, value, parent.Id));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Title\"={"bad\u0001title"} WHERE \"RequestId\"={parent.Id}"));
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"Description\"={"\r\n\t "} WHERE \"RequestId\"={parent.Id}"));
        var ownWorkflow = WorkflowDefinition.CreateDraft(a.Tenant, "Request workflow", WorkflowBusinessType.Request, DateTimeOffset.UtcNow);
        var requestVersion = WorkflowVersion.CreateDraft(ownWorkflow.Id, 1);
        var taskWorkflow = WorkflowDefinition.CreateDraft(a.Tenant, "Task workflow", WorkflowBusinessType.Task, DateTimeOffset.UtcNow);
        var taskVersion = WorkflowVersion.CreateDraft(taskWorkflow.Id, 1);
        db.AddRange(ownWorkflow, requestVersion, taskWorkflow, taskVersion); await db.SaveChangesAsync();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"WorkflowVersionId\"={requestVersion.Id} WHERE \"RequestId\"={parent.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkflowVersion\" SET \"Status\"='PUBLISHED', \"PublishedAt\"=now() WHERE \"WorkflowVersionId\" IN ({requestVersion.Id},{taskVersion.Id})");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"WorkflowVersionId\"={taskVersion.Id} WHERE \"RequestId\"={parent.Id}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"WorkflowVersionId\"={requestVersion.Id} WHERE \"RequestId\"={parent.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"WorkflowVersionId\"=NULL WHERE \"RequestId\"={parent.Id}"));
        var profile = SlaProfile.CreateDraft(b.Tenant, "Foreign SLA"); var calendar = BusinessCalendar.Create(b.Tenant, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var version = SlaVersion.CreateSnapshot(profile.Id, 1, 60, 45, calendar.Id);
        foreign.AddRange(profile, calendar, version); await foreign.SaveChangesAsync();
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"SLAVersionId\"={version.Id} WHERE \"RequestId\"={parent.Id}"));
        var ownProfile = SlaProfile.CreateDraft(a.Tenant, "Own SLA"); var ownCalendar = BusinessCalendar.Create(a.Tenant, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var ownVersion = SlaVersion.CreateSnapshot(ownProfile.Id, 1, 60, 45, ownCalendar.Id);
        db.AddRange(ownProfile, ownCalendar, ownVersion); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"SLAVersionId\"={ownVersion.Id} WHERE \"RequestId\"={parent.Id}");
        await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"SLAVersionId\"=NULL WHERE \"RequestId\"={parent.Id}"));
        Assert.Equal(parent.Id, (await db.Requests.AsNoTracking().SingleAsync(r => r.Id == child.Id)).ParentRequestId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Request\" SET \"DeletedAt\"=now() WHERE \"RequestId\"={child.Id}");
        Assert.False(await db.Requests.AnyAsync(r => r.Id == child.Id));
        Assert.True(await db.Requests.IgnoreQueryFilters().AnyAsync(r => r.Id == child.Id));
    }

    [Fact]
    public async Task Migration_round_trips_empty_and_refuses_to_discard_request_history()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261003022840_ServiceCatalogFoundation");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await SeedAsync(fixture); await using var db = fixture.Create(a.User, a.Tenant);
            var request = Draft(a); db.Requests.Add(request); await db.SaveChangesAsync();
            await Check(() => migration.GetService<IMigrator>().MigrateAsync("20261003022840_ServiceCatalogFoundation"));
            Assert.Contains("20261003165823_RequestDraftFoundation", await migration.Database.GetAppliedMigrationsAsync());
            Assert.Equal(request.Id, (await db.Requests.SingleAsync()).Id);
            await Check(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Request\" WHERE \"RequestId\"={request.Id}"));
        }
        finally { await fixture.DisposeAsync(); }
    }

    private sealed record Seed(Guid Tenant, Guid User, Guid Service, Guid Category);
    private static WorkRequest Draft(Seed seed, Guid? parent = null) => WorkRequest.CreateDraft(seed.Tenant, seed.User, seed.Service, seed.Category,
        "Request", "<script>literal</script>\nDetails", DateTimeOffset.UtcNow, parentRequestId: parent);
    private static Task<int> InsertDraftAsync(BizFlowDbContext db, Seed seed, Guid? parent = null, Guid? revision = null) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "Request" ("RequestId","TenantId","RequesterId","ServiceId","CategoryId","ParentRequestId","RevisedFromRequestId","Title","Description","CreatedAt","UpdatedAt")
        VALUES ({Guid.CreateVersion7()},{seed.Tenant},{seed.User},{seed.Service},{seed.Category},{parent},{revision},'Title','Details',now(),now())
        """);
    private static async Task<Seed> SeedAsync(PostgresFixture fixture)
    {
        var tenant = await fixture.SeedTenantAsync(); await using var db = fixture.Create(tenant.UserId, tenant.TenantId);
        var service = InternalService.Create(tenant.TenantId, "IT", "Support", null, true, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, "GENERAL", "General"); db.AddRange(service, category); await db.SaveChangesAsync();
        return new(tenant.TenantId, tenant.UserId, service.Id, category.Id);
    }
    private static async Task Check(Func<Task> action) => Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);
}
