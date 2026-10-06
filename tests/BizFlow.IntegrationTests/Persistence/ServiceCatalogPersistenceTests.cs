using BizFlow.Application.Common;
using BizFlow.Domain.Services;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class ServiceCatalogPersistenceTests(PostgresFixture database)
{
    [Fact]
    public async Task Services_and_categories_are_tenant_scoped_and_codes_are_unique_in_their_owning_scope()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var own = database.Create(a.UserId, a.TenantId); await using var other = database.Create(b.UserId, b.TenantId);
        var service = InternalService.Create(a.TenantId, "IT", "Support", null, true, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, "DEVICE", "Devices");
        own.AddRange(service, category); await own.SaveChangesAsync();
        var foreign = InternalService.Create(b.TenantId, "IT", "Private", null, true, DateTimeOffset.UtcNow);
        other.AddRange(foreign, ServiceCategory.Create(foreign.Id, "DEVICE", "Private devices")); await other.SaveChangesAsync();
        Assert.Equal("Support", (await own.Services.SingleAsync()).Name); Assert.Equal(category.Id, (await own.ServiceCategories.SingleAsync()).Id);
        Assert.Equal("Private", (await other.Services.SingleAsync()).Name);
        own.Services.Add(InternalService.Create(a.TenantId, "IT", "Duplicate", null, true, DateTimeOffset.UtcNow));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => own.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.InnerException!).SqlState); own.ChangeTracker.Clear();
        own.ServiceCategories.Add(ServiceCategory.Create(service.Id, "DEVICE", "Duplicate"));
        Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => own.SaveChangesAsync())).InnerException); own.ChangeTracker.Clear();
        var second = InternalService.Create(a.TenantId, "HR", "Support", null, false, DateTimeOffset.UtcNow);
        own.AddRange(second, ServiceCategory.Create(second.Id, "DEVICE", "Devices")); await own.SaveChangesAsync();
        Assert.Equal(2, await own.Services.CountAsync()); Assert.Equal(2, await own.ServiceCategories.CountAsync());
        await using var anonymous = database.Create(null, a.TenantId); await using var platform = database.Create(database.PlatformUserId, null);
        Assert.Empty(await anonymous.Services.ToListAsync()); Assert.Empty(await anonymous.ServiceCategories.ToListAsync());
        Assert.Empty(await platform.Services.ToListAsync()); Assert.Empty(await platform.ServiceCategories.ToListAsync());
    }

    [Fact]
    public async Task Writes_reject_forged_tenants_foreign_parents_and_detached_ownership_forgery()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var own = database.Create(a.UserId, a.TenantId); await using var other = database.Create(b.UserId, b.TenantId);
        var foreign = InternalService.Create(b.TenantId, "IT", "Private", null, true, DateTimeOffset.UtcNow);
        other.Services.Add(foreign); await other.SaveChangesAsync();
        own.Services.Add(InternalService.Create(b.TenantId, "BAD", "Forged", null, true, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        own.ServiceCategories.Add(ServiceCategory.Create(foreign.Id, "BAD", "Forged"));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        // An attached object falsely marked as our tenant must not replace the persisted parent check.
        var forged = InternalService.Create(a.TenantId, "IT", "Forged parent", null, true, DateTimeOffset.UtcNow);
        own.Entry(forged).Property(s => s.Id).CurrentValue = foreign.Id; own.Attach(forged);
        own.ServiceCategories.Add(ServiceCategory.Create(foreign.Id, "BAD", "Forged child"));
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        var service = InternalService.Create(a.TenantId, "OWN", "Own", null, true, DateTimeOffset.UtcNow);
        own.Services.Add(service); await own.SaveChangesAsync();
        own.Entry(service).Property(s => s.ActiveWorkflowVersionId).CurrentValue = Guid.NewGuid();
        await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync()); own.ChangeTracker.Clear();
        own.Services.Remove(service); await Assert.ThrowsAsync<ApplicationFault>(() => own.SaveChangesAsync());
        Assert.Empty(await other.ServiceCategories.ToListAsync());
    }

    [Fact]
    public async Task Database_guards_protect_identity_plain_text_and_configuration_tenant_references()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId); await using var other = database.Create(b.UserId, b.TenantId);
        var service = InternalService.Create(a.TenantId, "IT", "Support", "Line one\nLine two", true, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, "DEVICE", "Devices"); db.AddRange(service, category); await db.SaveChangesAsync();
        var foreign = InternalService.Create(b.TenantId, "IT", "Private", null, true, DateTimeOffset.UtcNow);
        var sla = SlaProfile.CreateDraft(b.TenantId, "Private SLA");
        var calendar = BusinessCalendar.Create(b.TenantId, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var snapshot = SlaVersion.CreateSnapshot(sla.Id, 1, 60, 45, calendar.Id);
        other.AddRange(foreign, sla, calendar, snapshot); await other.SaveChangesAsync();
        var workflow = WorkflowDefinition.CreateDraft(a.TenantId, "Own draft", WorkflowBusinessType.Request, DateTimeOffset.UtcNow);
        var version = WorkflowVersion.CreateDraft(workflow.Id, 1); db.AddRange(workflow, version); await db.SaveChangesAsync();
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Service\" SET \"TenantId\"={b.TenantId} WHERE \"ServiceId\"={service.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ServiceCategory\" SET \"ServiceId\"={foreign.Id} WHERE \"ServiceCategoryId\"={category.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Service\" SET \"ActiveSLAVersionId\"={snapshot.Id} WHERE \"ServiceId\"={service.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Service\" SET \"ActiveWorkflowVersionId\"={version.Id} WHERE \"ServiceId\"={service.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Service\" SET \"Code\"=' IT ' WHERE \"ServiceId\"={service.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ServiceCategory\" SET \"Name\"=' ' WHERE \"ServiceCategoryId\"={category.Id}"));
        await AssertCheck(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Service\" SET \"Description\"={"bad\u0001text"} WHERE \"ServiceId\"={service.Id}"));
        Assert.Null((await db.Services.AsNoTracking().SingleAsync()).ActiveSlaVersionId);
        Assert.Equal("Line one\nLine two", (await db.Services.AsNoTracking().SingleAsync()).Description);
    }

    [Fact]
    public async Task Migration_reapplies_empty_and_refuses_to_destroy_existing_service_configuration()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var db = fixture.Create(null, null);
            await db.GetService<IMigrator>().MigrateAsync("20261002182914_SlaSnapshotFoundation");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            var tenant = await fixture.SeedTenantAsync(); await using var scoped = fixture.Create(tenant.UserId, tenant.TenantId);
            var role = Role.CreateCustom(tenant.TenantId, "Service creator", DateTimeOffset.UtcNow); scoped.Roles.Add(role); await scoped.SaveChangesAsync();
            var grant = new RolePermission(role.Id, (await scoped.Permissions.SingleAsync(p => p.Code == "service.create")).Id);
            scoped.RolePermissions.Add(grant); await scoped.SaveChangesAsync();
            await AssertCheck(() => db.GetService<IMigrator>().MigrateAsync("20261002182914_SlaSnapshotFoundation"));
            Assert.True(await scoped.RolePermissions.AnyAsync(p => p.RoleId == role.Id));
            scoped.RolePermissions.Remove(grant); await scoped.SaveChangesAsync();
            var service = InternalService.Create(tenant.TenantId, "RETAIN", "Preserved", null, true, DateTimeOffset.UtcNow);
            scoped.AddRange(service, ServiceCategory.Create(service.Id, "RETAIN", "Preserved category")); await scoped.SaveChangesAsync();
            await AssertCheck(() => db.GetService<IMigrator>().MigrateAsync("20261002182914_SlaSnapshotFoundation"));
            Assert.Equal("Preserved", (await scoped.Services.SingleAsync()).Name); Assert.Single(await scoped.ServiceCategories.ToListAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static async Task AssertCheck(Func<Task> action) =>
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(action)).SqlState);

    [Fact]
    public async Task Update_permission_has_only_the_approved_default_grant_and_rollback_preserves_custom_configuration()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync(); await using var migration = fixture.Create(null, null);
            await migration.GetService<IMigrator>().MigrateAsync("20261003165823_RequestDraftFoundation");
            await migration.Database.MigrateAsync(); await migration.Database.MigrateAsync();
            var a = await fixture.SeedTenantAsync(); await using var db = fixture.Create(a.UserId, a.TenantId);
            var permission = await db.Permissions.SingleAsync(p => p.Code == "service.update");
            Assert.Equal(PermissionScope.Tenant, permission.ScopeType);
            var defaultGrant = Assert.Single(await db.RolePermissions.Where(r => r.PermissionId == permission.Id).ToListAsync());
            var systemRole = await db.Roles.SingleAsync(r => r.Id == defaultGrant.RoleId); Assert.True(systemRole.IsSystem); Assert.Equal("COMPANY_ADMIN", systemRole.Name);
            var custom = Role.CreateCustom(a.TenantId, "Service editor", DateTimeOffset.UtcNow); db.Roles.Add(custom); await db.SaveChangesAsync();
            db.RolePermissions.Add(new(custom.Id, permission.Id)); await db.SaveChangesAsync();
            await AssertCheck(() => migration.GetService<IMigrator>().MigrateAsync("20261003165823_RequestDraftFoundation"));
            Assert.True(await db.RolePermissions.AnyAsync(r => r.RoleId == custom.Id && r.PermissionId == permission.Id));
            Assert.Contains("20261003172253_ServiceMetadataUpdatePermission", await migration.Database.GetAppliedMigrationsAsync());
        }
        finally { await fixture.DisposeAsync(); }
    }
}
