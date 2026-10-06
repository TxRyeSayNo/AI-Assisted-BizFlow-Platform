using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceMetadataUpdatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a10400-0000-7000-8000-000000000001','service.update','service','update','TENANT');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a10400-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId"='01a10400-0000-7000-8000-000000000001'
                        AND "RoleId"<>'019f7f8a-0000-7000-8000-000000000002') THEN
                        RAISE EXCEPTION 'Cannot discard configured service update grants' USING ERRCODE='23514';
                    END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId"='01a10400-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId"='01a10400-0000-7000-8000-000000000001';
                """);
        }
    }
}
