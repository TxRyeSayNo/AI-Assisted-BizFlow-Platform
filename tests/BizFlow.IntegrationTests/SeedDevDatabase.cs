using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BizFlow.IntegrationTests;

public sealed class SeedDevDatabase
{
    private sealed record Context(Guid? UserId, Guid? TenantId) : ITenantContext;

    [Fact]
    public async Task Seed_Dev_Database_With_Accounts()
    {
        // The default matches deploy/.env.example, which is what docker compose brings up. A
        // machine whose local database differs overrides it with ConnectionStrings__BizFlow.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BizFlow")
            ?? "Host=127.0.0.1;Port=5432;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret";
        var options = new DbContextOptionsBuilder<BizFlowDbContext>()
            .UseBizFlowPostgres(connectionString)
            .Options;

        await using var db = new BizFlowDbContext(options, new Context(null, null));

        // This seeds a developer's own database for hand testing; it asserts nothing about the
        // product. With no such database reachable there is nothing to seed, so it stands down
        // instead of reddening a suite whose real checks run against their own containers.
        if (!await ReachableAsync(db)) return;

        var now = DateTimeOffset.UtcNow;
        var hasher = new PasswordHasher<UserAccount>();

        // 1. Company
        var companyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var employeeUserId = Guid.NewGuid();

        // Check if tenant already exists
        var existingTenantCount = await db.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM "Tenant" WHERE "TenantKey" = 'bizflow-corp'""").SingleAsync();
        if (existingTenantCount > 0)
        {
            return;
        }

        // Insert Company
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Company" ("CompanyId", "Code", "Name", "ContactEmail", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({companyId}, 'BIZFLOW-CORP', 'BizFlow Corporation', 'admin@bizflow.local', 'ACTIVE'::company_status, {now}, {now})
            ON CONFLICT ("Code") DO NOTHING;
            """);

        // Insert Tenant
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Tenant" ("TenantId", "CompanyId", "TenantKey", "Name", "Status", "TimeZone", "CreatedAt", "UpdatedAt")
            VALUES ({tenantId}, {companyId}, 'bizflow-corp', 'BizFlow Corporation', 'ACTIVE'::tenant_status, 'UTC', {now}, {now})
            ON CONFLICT ("TenantKey") DO NOTHING;
            """);

        // Insert Department
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({departmentId}, {tenantId}, 'IT', 'Information Technology', 'ACTIVE'::record_status, {now}, {now})
            ON CONFLICT ("TenantId", "Code") DO NOTHING;
            """);

        // Create Users & Password Hashes
        var dummyAdmin = UserAccount.CreateTenantUser(tenantId, "ADMIN001", "admin@bizflow.local", "Administrator", "placeholder", now, departmentId);
        var adminHash = hasher.HashPassword(dummyAdmin, "Password123!");

        var dummyEmp = UserAccount.CreateTenantUser(tenantId, "EMP001", "employee@bizflow.local", "Employee", "placeholder", now, departmentId);
        var empHash = hasher.HashPassword(dummyEmp, "Password123!");

        // Insert Admin User
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
                "Email", "NormalizedEmail", "FullName", "PasswordHash", "SecurityStamp", "DepartmentId", "Status",
                "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
            VALUES ({adminUserId}, {tenantId}, FALSE, 'ADMIN001', 'ADMIN001', 'admin@bizflow.local', 'ADMIN@BIZFLOW.LOCAL',
                'Administrator', {adminHash}, {Guid.NewGuid().ToString("N")}, {departmentId}, 'ACTIVE'::user_status,
                0, FALSE, {now}, {now})
            ON CONFLICT ("TenantId", "NormalizedEmployeeCode") DO UPDATE
            SET "PasswordHash" = {adminHash}, "Status" = 'ACTIVE'::user_status, "MustChangePassword" = FALSE;
            """);

        // Insert Employee User
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
                "Email", "NormalizedEmail", "FullName", "PasswordHash", "SecurityStamp", "DepartmentId", "Status",
                "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
            VALUES ({employeeUserId}, {tenantId}, FALSE, 'EMP001', 'EMP001', 'employee@bizflow.local', 'EMPLOYEE@BIZFLOW.LOCAL',
                'Employee', {empHash}, {Guid.NewGuid().ToString("N")}, {departmentId}, 'ACTIVE'::user_status,
                0, FALSE, {now}, {now})
            ON CONFLICT ("TenantId", "NormalizedEmployeeCode") DO UPDATE
            SET "PasswordHash" = {empHash}, "Status" = 'ACTIVE'::user_status, "MustChangePassword" = FALSE;
            """);

        // Find roles
        var adminRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        var managerRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.IsSystem && r.Name == "MANAGER");
        var employeeRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.IsSystem && r.Name == "EMPLOYEE");

        // Assign Roles
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRole" ("UserId", "RoleId")
            VALUES ({adminUserId}, {adminRole.Id}), ({adminUserId}, {managerRole.Id}), ({employeeUserId}, {employeeRole.Id})
            ON CONFLICT DO NOTHING;
            """);

        // Setup ManagementScope for Admin on Department
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {tenantId}, {adminUserId}, {departmentId}, TRUE, {adminUserId}, {now})
            ON CONFLICT DO NOTHING;
            """);

        // Seed Platform Administrator (platform@bizflow.local / Password123!)
        //
        // A platform administrator may already be there under a different id — from an earlier run
        // of this seeder or from the deploy scripts — and what collides then is the employee code,
        // not the id. So the account is found by its code first, and only created when it is really
        // missing; the role assignment then goes to whichever account is the live one.
        var platformRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.IsSystem && r.Name == "PLATFORM_ADMIN");
        var existing = await db.Database
            .SqlQueryRaw<Guid>("""SELECT "UserId" AS "Value" FROM "User" WHERE "NormalizedEmployeeCode" = 'SUPERADMIN' LIMIT 1""")
            .ToListAsync();
        var platformUserId = existing.Count > 0 ? existing[0] : Guid.Parse("019f8000-0000-7000-8000-000000000001");
        if (existing.Count == 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
                    "Email", "NormalizedEmail", "FullName", "PasswordHash", "SecurityStamp", "DepartmentId", "Status",
                    "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
                VALUES ({platformUserId}, NULL, TRUE, 'SUPERADMIN', 'SUPERADMIN', 'platform@bizflow.local', 'PLATFORM@BIZFLOW.LOCAL',
                    'Platform Administrator', {adminHash}, {Guid.NewGuid().ToString("N")}, NULL, 'ACTIVE'::user_status,
                    0, FALSE, {now}, {now})
                ON CONFLICT ("UserId") DO NOTHING;
                """);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRole" ("UserId", "RoleId")
            VALUES ({platformUserId}, {platformRole.Id})
            ON CONFLICT DO NOTHING;
            """);
    }

    /// <summary>
    /// Whether the configured development database answers. The connection retries on transient
    /// faults, so a refused connection surfaces as an exception rather than a false; either way the
    /// answer here is simply "no".
    /// </summary>
    private static async Task<bool> ReachableAsync(BizFlowDbContext db)
    {
        try
        {
            return await db.Database.CanConnectAsync();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return false;
        }
    }
}

