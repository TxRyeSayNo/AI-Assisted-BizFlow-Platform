using System;
using BizFlow.Domain.Sla;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaProfileCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .Annotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .Annotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .Annotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .Annotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .Annotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED")
                .OldAnnotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .OldAnnotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .OldAnnotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .OldAnnotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .OldAnnotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED");

            migrationBuilder.CreateTable(
                name: "SLAProfile",
                columns: table => new
                {
                    SLAProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<SlaProfileStatus>(type: "sla_profile_status", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SLAProfile", x => x.SLAProfileId);
                    table.CheckConstraint("CK_SLAProfile_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_SLAProfile_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SLAProfile_TenantId_Status_Name",
                table: "SLAProfile",
                columns: new[] { "TenantId", "Status", "Name" });
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_sla_profile_identity() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."SLAProfileId" IS DISTINCT FROM OLD."SLAProfileId" THEN
                        RAISE EXCEPTION 'SLA profile identity and tenant are immutable' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sla_profile_identity BEFORE UPDATE ON "SLAProfile"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_sla_profile_identity();
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a10259-0000-7000-8000-000000000001','sla.read','sla','read','TENANT'),
                    ('01a10259-0000-7000-8000-000000000002','sla.configure','sla','configure','TENANT');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a10259-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a10259-0000-7000-8000-000000000002');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SLAProfile") OR EXISTS
                        (SELECT 1 FROM "RolePermission" rp JOIN "Role" r ON r."RoleId"=rp."RoleId"
                         WHERE rp."PermissionId" IN ('01a10259-0000-7000-8000-000000000001','01a10259-0000-7000-8000-000000000002')
                         AND (NOT r."IsSystem" OR r."Name"<>'COMPANY_ADMIN')) THEN
                        RAISE EXCEPTION 'Cannot discard SLA profiles or configured permission grants' USING ERRCODE='23514';
                    END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a10259-0000-7000-8000-000000000001','01a10259-0000-7000-8000-000000000002');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a10259-0000-7000-8000-000000000001','01a10259-0000-7000-8000-000000000002');
                DROP TRIGGER sla_profile_identity ON "SLAProfile";
                DROP FUNCTION bizflow_sla_profile_identity();
                """);
            migrationBuilder.DropTable(
                name: "SLAProfile");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .Annotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .Annotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .Annotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .Annotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED")
                .OldAnnotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .OldAnnotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .OldAnnotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .OldAnnotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .OldAnnotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED");
        }
    }
}
