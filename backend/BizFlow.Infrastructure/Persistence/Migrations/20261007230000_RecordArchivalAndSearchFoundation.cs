using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordArchivalAndSearchFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Search & Archival Indexes
                CREATE INDEX IF NOT EXISTS "IX_Task_Tenant_DeletedAt" ON "Task" ("TenantId", "DeletedAt");
                CREATE INDEX IF NOT EXISTS "IX_Request_Tenant_DeletedAt" ON "Request" ("TenantId", "DeletedAt");

                -- Permissions
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a14000-0000-7000-8000-000000000006','records.archive','collaboration','archive','TENANT'),
                    ('01a14000-0000-7000-8000-000000000007','records.search','collaboration','search','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000006'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000007'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000006'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000007'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000007');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Task_Tenant_DeletedAt";
                DROP INDEX IF EXISTS "IX_Request_Tenant_DeletedAt";
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000006', '01a14000-0000-7000-8000-000000000007');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000006', '01a14000-0000-7000-8000-000000000007');
                """);
        }
    }
}
