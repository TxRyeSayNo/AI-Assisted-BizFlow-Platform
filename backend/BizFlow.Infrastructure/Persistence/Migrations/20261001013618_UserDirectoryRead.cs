using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserDirectoryRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SSS §5 Organization R for tenant roles, derived catalog per approved A-10.
            // No platform-to-tenant read bypass, accounts or role assignments are introduced.
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
                VALUES ('01a0f520-0000-7000-8000-000000000001', 'users.read', 'users', 'read', 'TENANT');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000001'),
                ('019f7f8a-0000-7000-8000-000000000003', '01a0f520-0000-7000-8000-000000000001'),
                ('019f7f8a-0000-7000-8000-000000000004', '01a0f520-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId" = '01a0f520-0000-7000-8000-000000000001'
                        AND "RoleId" NOT IN ('019f7f8a-0000-7000-8000-000000000002',
                            '019f7f8a-0000-7000-8000-000000000003', '019f7f8a-0000-7000-8000-000000000004'))
                    THEN RAISE EXCEPTION 'Cannot roll back a directory permission configured in custom roles'; END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId" = '01a0f520-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId" = '01a0f520-0000-7000-8000-000000000001';
                """);
        }
    }
}
