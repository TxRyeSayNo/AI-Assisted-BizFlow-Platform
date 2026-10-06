using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditReadPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                ('01a0f620-0000-7000-8000-000000000001', 'audit.read', 'audit', 'read', 'TENANT'),
                ('01a0f620-0000-7000-8000-000000000002', 'platform.audit.read', 'platform', 'audit.read', 'PLATFORM');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f620-0000-7000-8000-000000000001'),
                ('019f7a64-0000-7000-8000-000000000002', '01a0f620-0000-7000-8000-000000000002');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RolePermission" rp JOIN "Role" r ON r."RoleId" = rp."RoleId"
                        WHERE rp."PermissionId" IN ('01a0f620-0000-7000-8000-000000000001', '01a0f620-0000-7000-8000-000000000002')
                        AND NOT r."IsSystem")
                    THEN RAISE EXCEPTION 'Cannot remove audit permissions configured in custom roles'; END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a0f620-0000-7000-8000-000000000001', '01a0f620-0000-7000-8000-000000000002');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a0f620-0000-7000-8000-000000000001', '01a0f620-0000-7000-8000-000000000002');
                """);
        }
    }
}
