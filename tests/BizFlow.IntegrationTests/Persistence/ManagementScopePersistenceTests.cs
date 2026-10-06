using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Authentication;
using BizFlow.Infrastructure.Audit;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class ManagementScopePersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Scope_queries_fail_closed_and_isolate_tenant_configuration()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var department = await AddDepartmentAsync(db, a.TenantId);
        var scope = ManagementScope.Create(a.TenantId, a.UserId, department.Id, true, a.UserId, DateTimeOffset.UtcNow);
        db.ManagementScopes.Add(scope); await db.SaveChangesAsync();
        Assert.Equal(scope.Id, (await db.ManagementScopes.SingleAsync()).Id);
        await using var other = fixture.Create(b.UserId, b.TenantId);
        await using var anonymous = fixture.Create(null, a.TenantId);
        await using var platform = fixture.Create(fixture.PlatformUserId, null);
        Assert.Empty(await other.ManagementScopes.ToListAsync());
        Assert.Empty(await anonymous.ManagementScopes.ToListAsync());
        Assert.Empty(await platform.ManagementScopes.ToListAsync());
    }

    [Fact]
    public async Task Foreign_user_department_creator_and_tenant_are_rejected_by_EF_and_SQL()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        await using var other = fixture.Create(b.UserId, b.TenantId);
        var department = await AddDepartmentAsync(db, a.TenantId);
        var foreign = await AddDepartmentAsync(other, b.TenantId);
        var invalid = new[]
        {
            ManagementScope.Create(a.TenantId, b.UserId, department.Id, true, a.UserId, DateTimeOffset.UtcNow),
            ManagementScope.Create(a.TenantId, a.UserId, foreign.Id, true, a.UserId, DateTimeOffset.UtcNow),
            ManagementScope.Create(a.TenantId, a.UserId, department.Id, true, b.UserId, DateTimeOffset.UtcNow),
            ManagementScope.Create(b.TenantId, a.UserId, foreign.Id, true, a.UserId, DateTimeOffset.UtcNow),
            ManagementScope.Create(a.TenantId, fixture.PlatformUserId, department.Id, true, a.UserId, DateTimeOffset.UtcNow)
        };
        foreach (var scope in invalid)
        {
            db.ManagementScopes.Add(scope);
            Assert.Equal("PERSISTENCE.SCOPE_DENIED", (await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync())).Code);
            db.ChangeTracker.Clear();
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertSqlAsync(db, scope));
            Assert.Contains(error.SqlState, new[] { PostgresErrorCodes.CheckViolation, PostgresErrorCodes.ForeignKeyViolation });
        }
        Assert.Empty(await db.ManagementScopes.ToListAsync());
    }

    [Fact]
    public async Task Creator_cannot_be_spoofed_and_scope_ownership_cannot_be_transferred()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var department = await AddDepartmentAsync(db, a.TenantId);
        var colleague = UserAccount.CreateTenantUser(a.TenantId, "OTHER", "other@example.test", "Other", "test-hash", DateTimeOffset.UtcNow);
        db.Users.Add(colleague); await db.SaveChangesAsync();
        db.ManagementScopes.Add(ManagementScope.Create(a.TenantId, a.UserId, department.Id, false, colleague.Id, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var valid = ManagementScope.Create(a.TenantId, a.UserId, department.Id, false, a.UserId, DateTimeOffset.UtcNow);
        db.ManagementScopes.Add(valid); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ManagementScope" SET "UserId" = {colleague.Id} WHERE "ManagementScopeId" = {valid.Id}
            """));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ManagementScope" SET "CreatedBy" = {colleague.Id} WHERE "ManagementScopeId" = {valid.Id}
            """));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ManagementScope" SET "CreatedAt" = "CreatedAt" + interval '1 day' WHERE "ManagementScopeId" = {valid.Id}
            """));
        var duplicate = ManagementScope.Create(a.TenantId, a.UserId, department.Id, true, a.UserId, DateTimeOffset.UtcNow);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => InsertSqlAsync(db, duplicate))).SqlState);
    }

    [Fact]
    public async Task Resolver_expands_only_configured_subject_roots_and_observes_live_changes()
    {
        var a = await fixture.SeedTenantAsync(); var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        await using var other = fixture.Create(b.UserId, b.TenantId);
        var parent = await AddDepartmentAsync(db, a.TenantId);
        var root = await AddDepartmentAsync(db, a.TenantId, parent.Id);
        var child = await AddDepartmentAsync(db, a.TenantId, root.Id);
        var grandchild = await AddDepartmentAsync(db, a.TenantId, child.Id);
        var sibling = await AddDepartmentAsync(db, a.TenantId, parent.Id);
        var directOnly = await AddDepartmentAsync(db, a.TenantId);
        await AddDepartmentAsync(db, a.TenantId, directOnly.Id);
        var foreign = await AddDepartmentAsync(other, b.TenantId);
        other.ManagementScopes.Add(ManagementScope.Create(b.TenantId, b.UserId, foreign.Id, true, b.UserId, DateTimeOffset.UtcNow));
        await other.SaveChangesAsync();
        var colleague = UserAccount.CreateTenantUser(a.TenantId, "COLLEAGUE", "colleague@example.test", "Colleague", "test-hash", DateTimeOffset.UtcNow);
        db.Users.Add(colleague); await db.SaveChangesAsync();
        var rootScope = ManagementScope.Create(a.TenantId, a.UserId, root.Id, true, a.UserId, DateTimeOffset.UtcNow);
        db.ManagementScopes.AddRange(rootScope,
            ManagementScope.Create(a.TenantId, a.UserId, directOnly.Id, false, a.UserId, DateTimeOffset.UtcNow),
            ManagementScope.Create(a.TenantId, colleague.Id, sibling.Id, true, a.UserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        // Login resolves by a verified identity before HTTP tenant context exists.
        await using var anonymous = fixture.Create(null, null);
        var resolver = new AccessSnapshotProvider(anonymous, TimeProvider.System);
        var resolved = await resolver.ResolveAsync(a.UserId, a.TenantId, default);
        Assert.NotNull(resolved);
        Assert.True(resolved.ManagedDepartmentIds.SetEquals([root.Id, child.Id, grandchild.Id, directOnly.Id]));
        Assert.Empty(resolved.Grants); // Configuration alone never grants tasks.assign.
        Assert.Null(await resolver.ResolveAsync(a.UserId, b.TenantId, default));
        // Test-only configuration changes until audited administration is implemented.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ManagementScope\" SET \"IncludeDescendants\" = FALSE WHERE \"ManagementScopeId\" = {rootScope.Id}");
        Assert.True((await resolver.ResolveAsync(a.UserId, a.TenantId, default))!.ManagedDepartmentIds.SetEquals([root.Id, directOnly.Id]));
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ManagementScope\" WHERE \"UserId\" = {a.UserId}");
        Assert.Empty((await resolver.ResolveAsync(a.UserId, a.TenantId, default))!.ManagedDepartmentIds);
        Assert.Empty((await resolver.ResolveAsync(fixture.PlatformUserId, null, default))!.ManagedDepartmentIds);
    }

    [Fact]
    public async Task Persisted_scope_and_permission_are_both_required_by_the_resource_policy()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var root = await AddDepartmentAsync(db, a.TenantId); var child = await AddDepartmentAsync(db, a.TenantId, root.Id);
        var outside = await AddDepartmentAsync(db, a.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = (SELECT "CompanyId" FROM "Tenant" WHERE "TenantId" = {a.TenantId})
            """);
        var role = Role.CreateCustom(a.TenantId, "Manager", DateTimeOffset.UtcNow);
        db.Roles.Add(role); await db.SaveChangesAsync(); db.UserRoles.Add(new(a.UserId, role.Id)); await db.SaveChangesAsync();
        var resolver = new AccessSnapshotProvider(db, TimeProvider.System);
        var scope = ManagementScope.Create(a.TenantId, a.UserId, root.Id, true, a.UserId, DateTimeOffset.UtcNow);
        db.ManagementScopes.Add(scope); await db.SaveChangesAsync();
        var resource = new ResourceScope(a.TenantId);
        var noGrant = (await resolver.ResolveAsync(a.UserId, a.TenantId, default))!;
        Assert.Equal(AccessDenial.PermissionDenied, ResourcePolicy.Evaluate(noGrant, "tasks.assign", resource, child.Id).Denial);
        var permissionId = Guid.CreateVersion7();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
            VALUES ({permissionId}, 'tasks.assign', 'tasks', 'assign', 'TENANT')
            ON CONFLICT ("Code") DO NOTHING
            """);
        permissionId = await db.Permissions.Where(p => p.Code == "tasks.assign").Select(p => p.Id).SingleAsync();
        db.RolePermissions.Add(new(role.Id, permissionId)); await db.SaveChangesAsync();
        var allowed = (await resolver.ResolveAsync(a.UserId, a.TenantId, default))!;
        Assert.True(ResourcePolicy.Evaluate(allowed, "tasks.assign", resource, child.Id).Allowed);
        Assert.Equal(AccessDenial.ManagementScopeDenied, ResourcePolicy.Evaluate(allowed, "tasks.assign", resource, outside.Id).Denial);
        var context = new Context(a.UserId, a.TenantId);
        var options = new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(fixture.ConnectionString).Options;
        var authorizer = new ResourceAuthorizer(context, resolver, new SecurityAuditWriter(options, context, TimeProvider.System));
        await authorizer.AuthorizeAsync("tasks.assign", resource, child.Id);
        var denied = await Assert.ThrowsAsync<ApplicationFault>(() => authorizer.AuthorizeAsync("tasks.assign", resource, outside.Id));
        Assert.Equal(FaultKind.Forbidden, denied.Kind); Assert.Equal("TASK.TARGET_OUT_OF_SCOPE", denied.Code);
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(a.UserId, audit.ActorId); Assert.Contains("ManagementScopeDenied", audit.MetadataJson!);
        Assert.DoesNotContain(outside.Id.ToString(), audit.MetadataJson!);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ManagementScope\" WHERE \"ManagementScopeId\" = {scope.Id}");
        Assert.Equal(AccessDenial.ManagementScopeDenied, ResourcePolicy.Evaluate((await resolver.ResolveAsync(a.UserId, a.TenantId, default))!, "tasks.assign", resource, child.Id).Denial);
        await InsertSqlAsync(db, scope);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {a.UserId}");
        Assert.Equal(AccessDenial.InactiveAccount, ResourcePolicy.Evaluate((await resolver.ResolveAsync(a.UserId, a.TenantId, default))!, "tasks.assign", resource, child.Id).Denial);
    }

    private static async Task<Department> AddDepartmentAsync(BizFlowDbContext db, Guid tenantId, Guid? parentId = null)
    {
        var department = Department.Create(tenantId, Guid.NewGuid().ToString("N"), "Department", parentId, DateTimeOffset.UtcNow);
        db.Departments.Add(department); await db.SaveChangesAsync(); return department;
    }

    private static Task<int> InsertSqlAsync(BizFlowDbContext db, ManagementScope scope) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
        VALUES ({scope.Id}, {scope.TenantId}, {scope.UserId}, {scope.DepartmentId}, {scope.IncludeDescendants}, {scope.CreatedBy}, {scope.CreatedAt})
        """);

    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;
}
