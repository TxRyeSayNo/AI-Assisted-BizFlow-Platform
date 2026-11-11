using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BizFlowDbContext))]
    [Migration("20261111100000_GranularUserManagementPermissions")]
    public partial class GranularUserManagementPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                ('01a0f520-0000-7000-8000-000000000002', 'users.create', 'users', 'create', 'TENANT'),
                ('01a0f520-0000-7000-8000-000000000003', 'users.import', 'users', 'import', 'TENANT'),
                ('01a0f520-0000-7000-8000-000000000004', 'users.update', 'users', 'update', 'TENANT'),
                ('01a0f520-0000-7000-8000-000000000005', 'users.status.update', 'users', 'status.update', 'TENANT'),
                ('01a0f520-0000-7000-8000-000000000006', 'users.roles.assign', 'users', 'roles.assign', 'TENANT')
                ON CONFLICT ("PermissionId") DO NOTHING;

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000002'),
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000003'),
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000004'),
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000005'),
                ('019f7f8a-0000-7000-8000-000000000002', '01a0f520-0000-7000-8000-000000000006')
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "PermissionId" IN (
                    '01a0f520-0000-7000-8000-000000000002',
                    '01a0f520-0000-7000-8000-000000000003',
                    '01a0f520-0000-7000-8000-000000000004',
                    '01a0f520-0000-7000-8000-000000000005',
                    '01a0f520-0000-7000-8000-000000000006'
                );
                DELETE FROM "Permission" WHERE "PermissionId" IN (
                    '01a0f520-0000-7000-8000-000000000002',
                    '01a0f520-0000-7000-8000-000000000003',
                    '01a0f520-0000-7000-8000-000000000004',
                    '01a0f520-0000-7000-8000-000000000005',
                    '01a0f520-0000-7000-8000-000000000006'
                );
                """);
        }
    }
}

