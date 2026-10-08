using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DashboardAndReportingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Reporting & Dashboard Permissions
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a14000-0000-7000-8000-000000000008','reports.manager','reports','manager','TENANT'),
                    ('01a14000-0000-7000-8000-000000000009','reports.company','reports','company','TENANT'),
                    ('01a14000-0000-7000-8000-00000000000a','reports.workload','reports','workload','TENANT'),
                    ('01a14000-0000-7000-8000-00000000000b','reports.sla','reports','sla','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    -- COMPANY_ADMIN
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000008'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000009'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-00000000000a'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-00000000000b'),
                    -- MANAGER
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000008'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-00000000000a'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-00000000000b');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "PermissionId" IN (
                    '01a14000-0000-7000-8000-000000000008',
                    '01a14000-0000-7000-8000-000000000009',
                    '01a14000-0000-7000-8000-00000000000a',
                    '01a14000-0000-7000-8000-00000000000b'
                );
                DELETE FROM "Permission" WHERE "PermissionId" IN (
                    '01a14000-0000-7000-8000-000000000008',
                    '01a14000-0000-7000-8000-000000000009',
                    '01a14000-0000-7000-8000-00000000000a',
                    '01a14000-0000-7000-8000-00000000000b'
                );
                """);
        }
    }
}
