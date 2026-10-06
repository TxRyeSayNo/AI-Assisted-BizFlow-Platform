using System.Text.Json;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TaskCreationPrerequisiteTests(PostgresFixture database)
{
    [Fact]
    public async Task Draft_checklist_and_creation_audit_persist_together_with_creator_and_tenant_attribution()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var now = DateTimeOffset.UtcNow;
        var task = WorkTask.CreateDraft(seed.TenantId, seed.UserId, "Audited draft", now);
        var item = TaskChecklistItem.Create(task.Id, "Verify outcome", 1);
        var audit = AuditLog.TaskCreated(task, [item], now);
        db.AddRange(task, item, audit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await db.AuditLogs.SingleAsync(a => a.ObjectId == task.Id && a.Action == "TASK.CREATED");
        Assert.Equal(seed.UserId, stored.ActorId); Assert.Equal(seed.TenantId, stored.TenantId);
        Assert.Equal(TaskState.Draft, (await db.WorkTasks.SingleAsync(t => t.Id == task.Id)).Status);
        Assert.Equal(item.Id, (await db.TaskChecklistItems.SingleAsync(c => c.TaskId == task.Id)).Id);
        using var json = JsonDocument.Parse(stored.AfterJson!);
        Assert.Equal("Audited draft", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(item.Id, json.RootElement.GetProperty("checklist")[0].GetProperty("checklistItemId").GetGuid());
        var other = await database.SeedTenantAsync();
        await using var foreign = database.Create(other.UserId, other.TenantId);
        Assert.Empty(await foreign.AuditLogs.Where(a => a.Id == audit.Id).ToArrayAsync());
        Assert.Empty(await foreign.WorkTasks.Where(t => t.Id == task.Id).ToArrayAsync());
        Assert.Empty(await foreign.TaskChecklistItems.Where(c => c.TaskId == task.Id).ToArrayAsync());
        await using var tenantless = database.Create(other.UserId, null);
        Assert.Empty(await tenantless.AuditLogs.Where(a => a.Id == audit.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Only_the_manager_system_role_receives_the_specified_creation_grant()
    {
        await using var db = database.Create(database.PlatformUserId, null);
        var grants = await (from p in db.Permissions.IgnoreQueryFilters()
            join rp in db.RolePermissions.IgnoreQueryFilters() on p.Id equals rp.PermissionId
            join role in db.Roles.IgnoreQueryFilters() on rp.RoleId equals role.Id
            where p.Code == "tasks.create"
            select new { role.Name, p.ScopeType }).ToArrayAsync();
        var grant = Assert.Single(grants);
        Assert.Equal("MANAGER", grant.Name); Assert.Equal(PermissionScope.Tenant, grant.ScopeType);
    }

    [Fact]
    public async Task Creation_permission_rollback_reapplies_but_refuses_to_discard_custom_grants()
    {
        var isolated = new PostgresFixture();
        try
        {
            await isolated.InitializeAsync();
            await using (var db = isolated.Create(isolated.PlatformUserId, null))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261005003438_ScopedTaskReadPermissions");
                Assert.False(await db.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Code == "tasks.create"));
                await db.Database.MigrateAsync();
            }
            var seed = await isolated.SeedTenantAsync();
            await using (var db = isolated.Create(seed.UserId, seed.TenantId))
            {
                var role = Role.CreateCustom(seed.TenantId, "Task creators", DateTimeOffset.UtcNow);
                db.Roles.Add(role); await db.SaveChangesAsync();
                var permission = await db.Permissions.SingleAsync(p => p.Code == "tasks.create");
                db.RolePermissions.Add(new(role.Id, permission.Id)); await db.SaveChangesAsync();
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261005003438_ScopedTaskReadPermissions"));
                Assert.Equal("23514", error.SqlState);
            }
            await using (var db = isolated.Create(seed.UserId, seed.TenantId))
                Assert.Contains("20261005055653_TaskCreationPermission", await db.Database.GetAppliedMigrationsAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }
}
