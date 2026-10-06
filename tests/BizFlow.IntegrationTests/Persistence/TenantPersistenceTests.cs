using BizFlow.Application.Common;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class TenantPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Default_queries_isolate_users_departments_and_audit_and_fail_closed_without_tenant()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync();
        await using var aDb = fixture.Create(a.UserId, a.TenantId);
        await using var bDb = fixture.Create(b.UserId, b.TenantId);
        bDb.Departments.Add(Department.Create(b.TenantId, "OPS", "Operations", null, DateTimeOffset.UtcNow));
        bDb.AuditLogs.Add(AuditLog.DeniedAccess(b.TenantId, b.UserId, "users.update", "PermissionDenied", DateTimeOffset.UtcNow));
        await bDb.SaveChangesAsync();
        Assert.Null(await aDb.Users.SingleOrDefaultAsync(u => u.Id == b.UserId));
        Assert.Empty(await aDb.Departments.ToListAsync());
        Assert.Empty(await aDb.AuditLogs.ToListAsync());
        Assert.Single(await aDb.Users.ToListAsync());

        await using var noTenant = fixture.Create(fixture.PlatformUserId, null);
        Assert.Empty(await noTenant.Users.ToListAsync());
        Assert.Empty(await noTenant.Departments.ToListAsync());
        Assert.Empty(await noTenant.AuditLogs.ToListAsync());
        await using var anonymous = fixture.Create(null, a.TenantId);
        Assert.Empty(await anonymous.Users.ToListAsync());
    }

    [Fact]
    public async Task Identity_uniqueness_is_case_insensitive_within_a_tenant_but_not_global()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync(); // Same employee code/email, different tenant: allowed.
        await using var db = fixture.Create(a.UserId, a.TenantId);
        db.Users.Add(UserAccount.CreateTenantUser(a.TenantId, "emp001", "different@example.test", "Duplicate Code",
            "test-hash", DateTimeOffset.UtcNow));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        await using var other = fixture.Create(b.UserId, b.TenantId);
        Assert.Single(await other.Users.ToListAsync());
    }

    [Fact]
    public async Task Tenantless_non_platform_account_is_rejected_by_database_constraint()
    {
        await using var db = fixture.Create(fixture.PlatformUserId, null);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
                "Email", "NormalizedEmail", "FullName", "PasswordHash", "SecurityStamp", "Status", "AccessFailedCount",
                "MustChangePassword", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.CreateVersion7()}, NULL, FALSE, 'INVALID', 'INVALID', 'invalid@example.test', 'INVALID@EXAMPLE.TEST',
                'Invalid account', 'test-hash', 'test-stamp', 'ACTIVE', 0, FALSE, now(), now())
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_User_AccountPlane", error.ConstraintName);
    }

    [Fact]
    public async Task A_foreign_tenant_write_is_rejected_before_SQL()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        db.Departments.Add(Department.Create(b.TenantId, "OPS", "Unauthorized", null, DateTimeOffset.UtcNow));
        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync());
        Assert.Equal("PERSISTENCE.SCOPE_DENIED", fault.Code);
    }

    [Fact]
    public async Task Parent_department_FK_cannot_cross_a_tenant_boundary()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync();
        await using var bDb = fixture.Create(b.UserId, b.TenantId);
        var parent = Department.Create(b.TenantId, "PARENT", "Foreign Parent", null, DateTimeOffset.UtcNow);
        bDb.Departments.Add(parent);
        await bDb.SaveChangesAsync();
        await using var aDb = fixture.Create(a.UserId, a.TenantId);
        aDb.Departments.Add(Department.Create(a.TenantId, "CHILD", "Child", parent.Id, DateTimeOffset.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => aDb.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Spoofed_detached_entity_cannot_update_foreign_tenant_even_with_known_xmin()
    {
        var a = await fixture.SeedTenantAsync();
        var b = await fixture.SeedTenantAsync();
        await using var bDb = fixture.Create(b.UserId, b.TenantId);
        var department = Department.Create(b.TenantId, "OPS", "Original", null, DateTimeOffset.UtcNow);
        bDb.Departments.Add(department);
        await bDb.SaveChangesAsync();
        var version = bDb.Entry(department).Property<uint>("Version").CurrentValue;

        await using var aDb = fixture.Create(a.UserId, a.TenantId);
        var forged = Department.Create(a.TenantId, "OPS", "Original", null, department.CreatedAt);
        aDb.Entry(forged).Property(x => x.Id).CurrentValue = department.Id;
        aDb.Attach(forged);
        aDb.Entry(forged).Property<uint>("Version").OriginalValue = version;
        aDb.Entry(forged).Property(x => x.Name).CurrentValue = "Compromised";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => aDb.SaveChangesAsync());
        await bDb.Entry(department).ReloadAsync();
        Assert.Equal("Original", department.Name);
    }

    [Fact]
    public async Task Audit_is_append_only_even_when_SQL_bypasses_EF()
    {
        var a = await fixture.SeedTenantAsync();
        await using var db = fixture.Create(a.UserId, a.TenantId);
        var audit = AuditLog.DeniedAccess(a.TenantId, a.UserId, "users.update", "PermissionDenied", DateTimeOffset.UtcNow);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"AuditLog\" WHERE \"AuditLogId\" = {audit.Id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"AuditLog\" SET \"Action\" = 'OVERWRITTEN' WHERE \"AuditLogId\" = {audit.Id}"));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Id == audit.Id));
    }

    [Fact]
    public async Task Refresh_rotation_has_one_winner_and_losing_successor_is_rolled_back()
    {
        var a = await fixture.SeedTenantAsync();
        var now = DateTimeOffset.UtcNow;
        await using var seed = fixture.Create(a.UserId, a.TenantId);
        var original = AuthenticationSession.Create(a.UserId, new string('a', 64), now, now.AddDays(1));
        seed.AuthenticationSessions.Add(original);
        await seed.SaveChangesAsync();
        await using var first = fixture.Create(a.UserId, a.TenantId);
        await using var second = fixture.Create(a.UserId, a.TenantId);
        var one = await first.AuthenticationSessions.SingleAsync(s => s.Id == original.Id);
        var two = await second.AuthenticationSessions.SingleAsync(s => s.Id == original.Id);
        first.AuthenticationSessions.Add(one.Rotate(new string('b', 64), now.AddMinutes(1)));
        second.AuthenticationSessions.Add(two.Rotate(new string('c', 64), now.AddMinutes(1)));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = fixture.Create(a.UserId, a.TenantId);
        Assert.Equal(2, await verify.AuthenticationSessions.CountAsync());
        Assert.Single(await verify.AuthenticationSessions.Where(s => s.RevokedAt == null).ToListAsync());
    }
}
