using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopedTaskReadPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a10900-0000-7000-8000-000000000001','tasks.read.tenant','tasks','read','TENANT'),
                    ('01a10900-0000-7000-8000-000000000002','tasks.read.managed','tasks','read','DEPARTMENT'),
                    ('01a10900-0000-7000-8000-000000000003','tasks.read.own','tasks','read','SELF'),
                    ('01a10900-0000-7000-8000-000000000004','tasks.read.assigned','tasks','read','ASSIGNED');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a10900-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a10900-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a10900-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a10900-0000-7000-8000-000000000004'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a10900-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a10900-0000-7000-8000-000000000004');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p USING ("PermissionId")
                        WHERE p."Code" IN ('tasks.read.tenant','tasks.read.managed','tasks.read.own','tasks.read.assigned')
                        AND NOT (
                            (p."Code"='tasks.read.tenant' AND rp."RoleId"='019f7f8a-0000-7000-8000-000000000002') OR
                            (p."Code"='tasks.read.managed' AND rp."RoleId"='019f7f8a-0000-7000-8000-000000000003') OR
                            (p."Code" IN ('tasks.read.own','tasks.read.assigned') AND rp."RoleId" IN
                                ('019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004'))
                        )) THEN RAISE EXCEPTION 'Cannot discard configured Task read grants' USING ERRCODE='23514';
                    END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId" IN (SELECT "PermissionId" FROM "Permission"
                    WHERE "Code" IN ('tasks.read.tenant','tasks.read.managed','tasks.read.own','tasks.read.assigned'));
                DELETE FROM "Permission" WHERE "Code" IN ('tasks.read.tenant','tasks.read.managed','tasks.read.own','tasks.read.assigned');
                """);
        }
    }
}
