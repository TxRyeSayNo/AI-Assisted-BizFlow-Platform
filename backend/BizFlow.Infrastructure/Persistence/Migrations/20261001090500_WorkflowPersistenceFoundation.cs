using System;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowPersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED");

            migrationBuilder.CreateTable(
                name: "Workflow",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<WorkflowBusinessType>(type: "workflow_business_type", nullable: false),
                    Status = table.Column<WorkflowStatus>(type: "workflow_status", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflow", x => x.WorkflowId);
                    table.ForeignKey(
                        name: "FK_Workflow_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowVersion",
                columns: table => new
                {
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<WorkflowVersionStatus>(type: "workflow_version_status", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVersion", x => x.WorkflowVersionId);
                    table.CheckConstraint("CK_WorkflowVersion_JsonObject", "jsonb_typeof(\"DefinitionJson\") = 'object'");
                    table.CheckConstraint("CK_WorkflowVersion_Number", "\"VersionNo\" > 0");
                    table.CheckConstraint("CK_WorkflowVersion_Publication", "(\"Status\" = 'DRAFT' AND \"PublishedAt\" IS NULL) OR (\"Status\" <> 'DRAFT' AND \"PublishedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkflowVersion_Workflow_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflow",
                        principalColumn: "WorkflowId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStep",
                columns: table => new
                {
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<WorkflowStepType>(type: "workflow_step_type", nullable: false),
                    OrderNo = table.Column<int>(type: "integer", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStep", x => x.WorkflowStepId);
                    table.CheckConstraint("CK_WorkflowStep_Code", "\"StepCode\" ~ '^[A-Z0-9_.-]{1,80}$'");
                    table.CheckConstraint("CK_WorkflowStep_JsonObject", "jsonb_typeof(\"ConfigJson\") = 'object'");
                    table.CheckConstraint("CK_WorkflowStep_Order", "\"OrderNo\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkflowStep_WorkflowVersion_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                columns: table => new
                {
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GuardJson = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransition", x => x.TransitionId);
                    table.CheckConstraint("CK_WorkflowTransition_JsonObject", "\"GuardJson\" IS NULL OR jsonb_typeof(\"GuardJson\") = 'object'");
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_WorkflowVersion_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_TenantId",
                table: "Workflow",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_WorkflowVersionId_OrderNo",
                table: "WorkflowStep",
                columns: new[] { "WorkflowVersionId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_WorkflowVersionId_StepCode",
                table: "WorkflowStep",
                columns: new[] { "WorkflowVersionId", "StepCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_WorkflowVersionId",
                table: "WorkflowTransition",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersion_WorkflowId_VersionNo",
                table: "WorkflowVersion",
                columns: new[] { "WorkflowId", "VersionNo" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_workflow_identity() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."BusinessType" IS DISTINCT FROM OLD."BusinessType" THEN
                        RAISE EXCEPTION 'Workflow tenant and business type are immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER workflow_identity BEFORE UPDATE ON "Workflow"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_identity();

                CREATE FUNCTION bizflow_workflow_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."Status" <> 'DRAFT' THEN
                        RAISE EXCEPTION 'Published workflow versions are immutable' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    IF NEW."WorkflowId" IS DISTINCT FROM OLD."WorkflowId" OR NEW."VersionNo" IS DISTINCT FROM OLD."VersionNo" THEN
                        RAISE EXCEPTION 'Workflow version identity is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER workflow_version_immutable BEFORE UPDATE OR DELETE ON "WorkflowVersion"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_version_immutable();

                CREATE FUNCTION bizflow_workflow_child_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_id uuid; parent_status workflow_version_status;
                BEGIN
                    IF TG_OP = 'UPDATE' AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId" THEN
                        RAISE EXCEPTION 'Workflow child ownership is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'DELETE' THEN parent_id := OLD."WorkflowVersionId";
                    ELSE parent_id := NEW."WorkflowVersionId"; END IF;
                    -- Same parent lock as publication: waiting writers must see the committed status.
                    SELECT "Status" INTO parent_status FROM "WorkflowVersion" WHERE "WorkflowVersionId" = parent_id FOR UPDATE;
                    IF parent_status IS NULL OR parent_status <> 'DRAFT' THEN
                        RAISE EXCEPTION 'Only draft workflow versions accept child mutations' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER workflow_step_immutable BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowStep"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_child_immutable();
                CREATE TRIGGER workflow_transition_immutable BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowTransition"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_child_immutable();

                CREATE FUNCTION bizflow_workflow_no_truncate() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Workflow history cannot be truncated' USING ERRCODE = '23514'; END $$;
                CREATE TRIGGER workflow_no_truncate BEFORE TRUNCATE ON "Workflow"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_workflow_no_truncate();
                CREATE TRIGGER workflow_version_no_truncate BEFORE TRUNCATE ON "WorkflowVersion"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_workflow_no_truncate();
                CREATE TRIGGER workflow_step_no_truncate BEFORE TRUNCATE ON "WorkflowStep"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_workflow_no_truncate();
                CREATE TRIGGER workflow_transition_no_truncate BEFORE TRUNCATE ON "WorkflowTransition"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_workflow_no_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Workflow") THEN
                        RAISE EXCEPTION 'Cannot roll back populated workflow configuration' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP FUNCTION bizflow_workflow_identity() CASCADE;
                DROP FUNCTION bizflow_workflow_version_immutable() CASCADE;
                DROP FUNCTION bizflow_workflow_child_immutable() CASCADE;
                DROP FUNCTION bizflow_workflow_no_truncate() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "WorkflowStep");

            migrationBuilder.DropTable(
                name: "WorkflowTransition");

            migrationBuilder.DropTable(
                name: "WorkflowVersion");

            migrationBuilder.DropTable(
                name: "Workflow");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .Annotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
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
        }
    }
}
