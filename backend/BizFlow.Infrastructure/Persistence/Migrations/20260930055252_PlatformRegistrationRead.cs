using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformRegistrationRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reference data only: no account, credential or implicit role assignment is created.
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
                VALUES ('019f7a64-0000-7000-8000-000000000001', 'platform.company-registrations.read', 'platform', 'company-registrations.read', 'PLATFORM');
                INSERT INTO "Role" ("RoleId", "TenantId", "Name", "IsSystem", "Status", "CreatedAt", "UpdatedAt")
                VALUES ('019f7a64-0000-7000-8000-000000000002', NULL, 'PLATFORM_ADMIN', TRUE, 'ACTIVE', '2026-09-30T00:00:00Z', '2026-09-30T00:00:00Z');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId")
                VALUES ('019f7a64-0000-7000-8000-000000000002', '019f7a64-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reject rollback once assigned rather than deleting live authorization assignments.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "UserRole" WHERE "RoleId" = '019f7a64-0000-7000-8000-000000000002')
                    THEN RAISE EXCEPTION 'Cannot roll back assigned platform reference data'; END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "RoleId" = '019f7a64-0000-7000-8000-000000000002'
                    AND "PermissionId" = '019f7a64-0000-7000-8000-000000000001';
                DELETE FROM "Role" WHERE "RoleId" = '019f7a64-0000-7000-8000-000000000002';
                DELETE FROM "Permission" WHERE "PermissionId" = '019f7a64-0000-7000-8000-000000000001';
                """);
        }
    }
}
