using System;
using BizFlow.Domain.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskDraftFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:request_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .Annotation("Npgsql:Enum:request_state", "CANCELLED,CLOSED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,RECEIVED,REJECTED,RESOLVED,ROUTED,SUBMITTED,WAITING_FOR_INFORMATION")
                .Annotation("Npgsql:Enum:service_category_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:service_status", "ACTIVE,DRAFT,INACTIVE")
                .Annotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .Annotation("Npgsql:Enum:task_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .Annotation("Npgsql:Enum:task_state", "ACCEPTED,ASSIGNED,CANCELLED,COMPLETED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,REJECTED,SUBMITTED")
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
                .OldAnnotation("Npgsql:Enum:request_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .OldAnnotation("Npgsql:Enum:request_state", "CANCELLED,CLOSED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,RECEIVED,REJECTED,RESOLVED,ROUTED,SUBMITTED,WAITING_FOR_INFORMATION")
                .OldAnnotation("Npgsql:Enum:service_category_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:service_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .OldAnnotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .OldAnnotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED");

            migrationBuilder.CreateTable(
                name: "Task",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<TaskPriority>(type: "task_priority", nullable: false, defaultValue: TaskPriority.Medium),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<TaskState>(type: "task_state", nullable: false, defaultValue: TaskState.Draft),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SLAVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Task", x => x.TaskId);
                    table.CheckConstraint("CK_Task_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "FK_Task_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Task_SLAVersion_SLAVersionId",
                        column: x => x.SLAVersionId,
                        principalTable: "SLAVersion",
                        principalColumn: "SLAVersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Task_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Task_User_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Task_WorkflowVersion_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskChecklistItem",
                columns: table => new
                {
                    ChecklistItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskChecklistItem", x => x.ChecklistItemId);
                    table.CheckConstraint("CK_TaskChecklistItem_Completion", "(\"IsCompleted\" AND \"CompletedBy\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedBy\" IS NULL AND \"CompletedAt\" IS NULL)");
                    table.CheckConstraint("CK_TaskChecklistItem_Order", "\"SortOrder\" > 0");
                    table.CheckConstraint("CK_TaskChecklistItem_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "FK_TaskChecklistItem_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Task",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskChecklistItem_User_CompletedBy",
                        column: x => x.CompletedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Task_CreatorId",
                table: "Task",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_RequestId",
                table: "Task",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_SLAVersionId",
                table: "Task",
                column: "SLAVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Task_TenantId_CreatorId_CreatedAt",
                table: "Task",
                columns: new[] { "TenantId", "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Task_TenantId_Status_Deadline",
                table: "Task",
                columns: new[] { "TenantId", "Status", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_Task_WorkflowVersionId",
                table: "Task",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChecklistItem_CompletedBy",
                table: "TaskChecklistItem",
                column: "CompletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TaskChecklistItem_TaskId_SortOrder",
                table: "TaskChecklistItem",
                columns: new[] { "TaskId", "SortOrder" });

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_task_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP IN ('DELETE','TRUNCATE') THEN
                        RAISE EXCEPTION 'Task history cannot be hard deleted' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' THEN
                        IF NEW."TaskId" IS DISTINCT FROM OLD."TaskId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
                           NEW."CreatorId" IS DISTINCT FROM OLD."CreatorId" OR NEW."RequestId" IS DISTINCT FROM OLD."RequestId" OR
                           NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt" THEN
                            RAISE EXCEPTION 'Task identity and request traceability are immutable' USING ERRCODE='23514';
                        END IF;
                        IF (OLD."WorkflowVersionId" IS NOT NULL AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId") OR
                           (OLD."SLAVersionId" IS NOT NULL AND NEW."SLAVersionId" IS DISTINCT FROM OLD."SLAVersionId") THEN
                            RAISE EXCEPTION 'Applied task configuration versions are immutable' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NEW."Title" ~ '[[:cntrl:]]' OR translate(NEW."Description", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Task text contains unsupported control characters' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."CreatorId" AND "TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Task creator must belong to its tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."RequestId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "Request" WHERE "RequestId"=NEW."RequestId" AND "TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Task source request must belong to its tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."WorkflowVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
                        WHERE v."WorkflowVersionId"=NEW."WorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND w."BusinessType"='TASK' AND v."Status"='PUBLISHED') THEN
                        RAISE EXCEPTION 'Applied workflow must be a published task workflow in its tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."SLAVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
                        WHERE v."SLAVersionId"=NEW."SLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Applied SLA must belong to the task tenant' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER task_guard BEFORE INSERT OR UPDATE OR DELETE ON "Task" FOR EACH ROW EXECUTE FUNCTION bizflow_task_guard();
                CREATE TRIGGER task_truncate_guard BEFORE TRUNCATE ON "Task" FOR EACH STATEMENT EXECUTE FUNCTION bizflow_task_guard();

                CREATE FUNCTION bizflow_task_checklist_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz;
                BEGIN
                    IF TG_OP IN ('DELETE','TRUNCATE') THEN
                        RAISE EXCEPTION 'Task checklist history cannot be hard deleted' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' AND (NEW."ChecklistItemId" IS DISTINCT FROM OLD."ChecklistItemId" OR NEW."TaskId" IS DISTINCT FROM OLD."TaskId") THEN
                        RAISE EXCEPTION 'Checklist identity and task ownership are immutable' USING ERRCODE='23514';
                    END IF;
                    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
                    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
                    IF NOT FOUND OR parent_deleted IS NOT NULL OR (TG_OP='INSERT' AND parent_status <> 'DRAFT') THEN
                        RAISE EXCEPTION 'Initial checklist requires a live draft task' USING ERRCODE='23514';
                    END IF;
                    IF NEW."Title" ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Checklist text contains unsupported control characters' USING ERRCODE='23514';
                    END IF;
                    IF NEW."CompletedBy" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "User" WHERE "UserId"=NEW."CompletedBy" AND "TenantId"=parent_tenant) THEN
                        RAISE EXCEPTION 'Checklist completer must belong to the task tenant' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER task_checklist_guard BEFORE INSERT OR UPDATE OR DELETE ON "TaskChecklistItem"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_task_checklist_guard();
                CREATE TRIGGER task_checklist_truncate_guard BEFORE TRUNCATE ON "TaskChecklistItem"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_task_checklist_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Task") OR EXISTS (SELECT 1 FROM "TaskChecklistItem") THEN
                        RAISE EXCEPTION 'Cannot discard task history during rollback' USING ERRCODE='23514';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "TaskChecklistItem");

            migrationBuilder.DropTable(
                name: "Task");

            migrationBuilder.Sql("DROP FUNCTION bizflow_task_checklist_guard(); DROP FUNCTION bizflow_task_guard();");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:audit_actor_type", "AI_AGENT,SYSTEM,USER")
                .Annotation("Npgsql:Enum:company_status", "ACTIVE,INACTIVE,PENDING,SUSPENDED")
                .Annotation("Npgsql:Enum:permission_scope", "ASSIGNED,DEPARTMENT,PLATFORM,SELF,TENANT")
                .Annotation("Npgsql:Enum:record_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:request_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .Annotation("Npgsql:Enum:request_state", "CANCELLED,CLOSED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,RECEIVED,REJECTED,RESOLVED,ROUTED,SUBMITTED,WAITING_FOR_INFORMATION")
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
                .OldAnnotation("Npgsql:Enum:request_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .OldAnnotation("Npgsql:Enum:request_state", "CANCELLED,CLOSED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,RECEIVED,REJECTED,RESOLVED,ROUTED,SUBMITTED,WAITING_FOR_INFORMATION")
                .OldAnnotation("Npgsql:Enum:service_category_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:service_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:sla_profile_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:task_priority", "CRITICAL,HIGH,LOW,MEDIUM")
                .OldAnnotation("Npgsql:Enum:task_state", "ACCEPTED,ASSIGNED,CANCELLED,COMPLETED,CONFIRMED,DRAFT,IN_PROGRESS,OVERDUE,REJECTED,SUBMITTED")
                .OldAnnotation("Npgsql:Enum:tenant_status", "ACTIVE,INACTIVE,PROVISIONING,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:workflow_business_type", "REQUEST,TASK")
                .OldAnnotation("Npgsql:Enum:workflow_status", "ACTIVE,DRAFT,INACTIVE")
                .OldAnnotation("Npgsql:Enum:workflow_step_type", "ACTION,APPROVAL,CONFIRMATION")
                .OldAnnotation("Npgsql:Enum:workflow_version_status", "DRAFT,PUBLISHED,RETIRED");
        }
    }
}
