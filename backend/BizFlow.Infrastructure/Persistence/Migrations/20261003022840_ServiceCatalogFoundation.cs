using System;
using BizFlow.Domain.Services;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:service_category_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:service_status", "ACTIVE,DRAFT,INACTIVE")
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
                .OldAnnotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .OldAnnotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .OldAnnotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED");

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<ServiceStatus>(type: "service_status", nullable: false),
                    ActiveWorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActiveSLAVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service", x => x.ServiceId);
                    table.CheckConstraint("CK_Service_Code", "length(btrim(\"Code\")) BETWEEN 1 AND 80");
                    table.CheckConstraint("CK_Service_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_Service_SLAVersion_ActiveSLAVersionId",
                        column: x => x.ActiveSLAVersionId,
                        principalTable: "SLAVersion",
                        principalColumn: "SLAVersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_WorkflowVersion_ActiveWorkflowVersionId",
                        column: x => x.ActiveWorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCategory",
                columns: table => new
                {
                    ServiceCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<ServiceCategoryStatus>(type: "service_category_status", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategory", x => x.ServiceCategoryId);
                    table.CheckConstraint("CK_ServiceCategory_Code", "length(btrim(\"Code\")) BETWEEN 1 AND 80");
                    table.CheckConstraint("CK_ServiceCategory_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_ServiceCategory_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Service_ActiveSLAVersionId",
                table: "Service",
                column: "ActiveSLAVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_ActiveWorkflowVersionId",
                table: "Service",
                column: "ActiveWorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_TenantId_Code",
                table: "Service",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Service_TenantId_Status_Name",
                table: "Service",
                columns: new[] { "TenantId", "Status", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategory_ServiceId_Code",
                table: "ServiceCategory",
                columns: new[] { "ServiceId", "Code" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_service_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP='UPDATE' AND (NEW."ServiceId" IS DISTINCT FROM OLD."ServiceId" OR
                        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt") THEN
                        RAISE EXCEPTION 'Service identity and tenant are immutable' USING ERRCODE='23514';
                    END IF;
                    IF NEW."Code"<>btrim(NEW."Code") OR NEW."Code" ~ '[[:cntrl:]]' OR NEW."Name" ~ '[[:cntrl:]]' OR
                        translate(coalesce(NEW."Description",''), E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Service metadata must be plain text with a trimmed code' USING ERRCODE='23514';
                    END IF;
                    IF NEW."ActiveWorkflowVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
                        WHERE v."WorkflowVersionId"=NEW."ActiveWorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND v."Status"='PUBLISHED') THEN
                        RAISE EXCEPTION 'Service workflow must be published in its tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."ActiveSLAVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
                        WHERE v."SLAVersionId"=NEW."ActiveSLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Service SLA must belong to its tenant' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER service_guard BEFORE INSERT OR UPDATE ON "Service" FOR EACH ROW EXECUTE FUNCTION bizflow_service_guard();
                CREATE FUNCTION bizflow_service_category_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP='UPDATE' AND (NEW."ServiceCategoryId" IS DISTINCT FROM OLD."ServiceCategoryId" OR NEW."ServiceId" IS DISTINCT FROM OLD."ServiceId") THEN
                        RAISE EXCEPTION 'Category identity and service are immutable' USING ERRCODE='23514';
                    END IF;
                    IF NEW."Code"<>btrim(NEW."Code") OR NEW."Code" ~ '[[:cntrl:]]' OR NEW."Name" ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Category metadata must be plain text with a trimmed code' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER service_category_guard BEFORE INSERT OR UPDATE ON "ServiceCategory" FOR EACH ROW EXECUTE FUNCTION bizflow_service_category_guard();
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a10302-0000-7000-8000-000000000001','service.create','service','create','TENANT');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a10302-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Service") OR EXISTS (SELECT 1 FROM "ServiceCategory") OR EXISTS
                        (SELECT 1 FROM "RolePermission" rp JOIN "Role" r ON r."RoleId"=rp."RoleId"
                        WHERE rp."PermissionId"='01a10302-0000-7000-8000-000000000001' AND (NOT r."IsSystem" OR r."Name"<>'COMPANY_ADMIN')) THEN
                        RAISE EXCEPTION 'Cannot discard configured services or categories' USING ERRCODE='23514';
                    END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId"='01a10302-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId"='01a10302-0000-7000-8000-000000000001';
                DROP TRIGGER service_category_guard ON "ServiceCategory";
                DROP FUNCTION bizflow_service_category_guard();
                DROP TRIGGER service_guard ON "Service";
                DROP FUNCTION bizflow_service_guard();
                """);
            migrationBuilder.DropTable(
                name: "ServiceCategory");

            migrationBuilder.DropTable(
                name: "Service");

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
                .OldAnnotation("Npgsql:Enum:service_category_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:service_status", "ACTIVE,DRAFT,INACTIVE")
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
