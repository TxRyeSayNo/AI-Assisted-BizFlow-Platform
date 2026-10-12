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
        var connectionString = "Host=localhost;Port=5432;Database=bizflow;Username=bizflow;Password=bizflow_local_secret_2026";
        var options = new DbContextOptionsBuilder<BizFlowDbContext>()
            .UseBizFlowPostgres(connectionString)
            .Options;

        await using var db = new BizFlowDbContext(options, new Context(null, null));

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
    }
}
