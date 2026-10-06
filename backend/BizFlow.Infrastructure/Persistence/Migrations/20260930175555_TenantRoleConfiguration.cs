using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantRoleConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration-owned reference data only. No users, tenant memberships or credentials.
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
                VALUES ('019f7f8a-0000-7000-8000-000000000001', 'roles.configure', 'roles', 'configure', 'TENANT');
                INSERT INTO "Role" ("RoleId", "TenantId", "Name", "IsSystem", "Status", "CreatedAt", "UpdatedAt") VALUES
                ('019f7f8a-0000-7000-8000-000000000002', NULL, 'COMPANY_ADMIN', TRUE, 'ACTIVE', '2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z'),
                ('019f7f8a-0000-7000-8000-000000000003', NULL, 'MANAGER', TRUE, 'ACTIVE', '2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z'),
                ('019f7f8a-0000-7000-8000-000000000004', NULL, 'EMPLOYEE', TRUE, 'ACTIVE', '2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId")
                VALUES ('019f7f8a-0000-7000-8000-000000000002', '019f7f8a-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "UserRole" WHERE "RoleId" IN
                        ('019f7f8a-0000-7000-8000-000000000002', '019f7f8a-0000-7000-8000-000000000003', '019f7f8a-0000-7000-8000-000000000004'))
                    OR EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId" = '019f7f8a-0000-7000-8000-000000000001'
                        AND "RoleId" <> '019f7f8a-0000-7000-8000-000000000002')
                    THEN RAISE EXCEPTION 'Cannot roll back assigned tenant role reference data'; END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "RoleId" = '019f7f8a-0000-7000-8000-000000000002'
                    AND "PermissionId" = '019f7f8a-0000-7000-8000-000000000001';
                DELETE FROM "Role" WHERE "RoleId" IN
                    ('019f7f8a-0000-7000-8000-000000000002', '019f7f8a-0000-7000-8000-000000000003', '019f7f8a-0000-7000-8000-000000000004');
                DELETE FROM "Permission" WHERE "PermissionId" = '019f7f8a-0000-7000-8000-000000000001';
                """);
        }
    }
}
