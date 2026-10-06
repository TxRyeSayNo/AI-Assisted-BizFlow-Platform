using System;
using BizFlow.Domain.Requests;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestDraftFoundation : Migration
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

            migrationBuilder.CreateTable(
                name: "Request",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisedFromRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<RequestPriority>(type: "request_priority", nullable: false, defaultValue: RequestPriority.Medium),
                    Status = table.Column<RequestState>(type: "request_state", nullable: false, defaultValue: RequestState.Draft),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SLAVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Request", x => x.RequestId);
                    table.CheckConstraint("CK_Request_Description", "length(btrim(\"Description\")) > 0");
                    table.CheckConstraint("CK_Request_Parent", "\"ParentRequestId\" IS NULL OR \"ParentRequestId\" <> \"RequestId\"");
                    table.CheckConstraint("CK_Request_Revision", "\"RevisedFromRequestId\" IS NULL OR \"RevisedFromRequestId\" <> \"RequestId\"");
                    table.CheckConstraint("CK_Request_Title", "length(btrim(\"Title\")) BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "FK_Request_Request_ParentRequestId",
                        column: x => x.ParentRequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_Request_RevisedFromRequestId",
                        column: x => x.RevisedFromRequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_SLAVersion_SLAVersionId",
                        column: x => x.SLAVersionId,
                        principalTable: "SLAVersion",
                        principalColumn: "SLAVersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_ServiceCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ServiceCategory",
                        principalColumn: "ServiceCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_User_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Request_WorkflowVersion_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Request_CategoryId",
                table: "Request",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_ParentRequestId",
                table: "Request",
                column: "ParentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequesterId",
                table: "Request",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RevisedFromRequestId",
                table: "Request",
                column: "RevisedFromRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_ServiceId",
                table: "Request",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_SLAVersionId",
                table: "Request",
                column: "SLAVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_TenantId_RequesterId_CreatedAt",
                table: "Request",
                columns: new[] { "TenantId", "RequesterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Request_TenantId_Status_CreatedAt",
                table: "Request",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Request_WorkflowVersionId",
                table: "Request",
                column: "WorkflowVersionId");
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_request_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP='DELETE' THEN
                        RAISE EXCEPTION 'Request history cannot be hard deleted' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' THEN
                        IF OLD."Status"='REJECTED' THEN
                            RAISE EXCEPTION 'Rejected requests are immutable; create a revision' USING ERRCODE='23514';
                        END IF;
                        IF NEW."RequestId" IS DISTINCT FROM OLD."RequestId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
                           NEW."RequesterId" IS DISTINCT FROM OLD."RequesterId" OR NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt" OR
                           NEW."ParentRequestId" IS DISTINCT FROM OLD."ParentRequestId" OR NEW."RevisedFromRequestId" IS DISTINCT FROM OLD."RevisedFromRequestId" THEN
                            RAISE EXCEPTION 'Request identity and historical links are immutable' USING ERRCODE='23514';
                        END IF;
                        IF (OLD."WorkflowVersionId" IS NOT NULL AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId") OR
                           (OLD."SLAVersionId" IS NOT NULL AND NEW."SLAVersionId" IS DISTINCT FROM OLD."SLAVersionId") THEN
                            RAISE EXCEPTION 'Applied request configuration versions are immutable' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NEW."Title" ~ '[[:cntrl:]]' OR NEW."Description" !~ '[^[:space:]]' OR
                       translate(NEW."Description", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Request text contains unsupported control characters' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."RequesterId" AND "TenantId"=NEW."TenantId") OR
                       NOT EXISTS (SELECT 1 FROM "Service" WHERE "ServiceId"=NEW."ServiceId" AND "TenantId"=NEW."TenantId") OR
                       NOT EXISTS (SELECT 1 FROM "ServiceCategory" WHERE "ServiceCategoryId"=NEW."CategoryId" AND "ServiceId"=NEW."ServiceId") THEN
                        RAISE EXCEPTION 'Request references must belong to its tenant and service' USING ERRCODE='23514';
                    END IF;
                    -- A parent must already exist, cannot be self, and cannot later be changed.
                    -- Thus parent cycles cannot be inserted or formed by concurrent reparenting.
                    IF NEW."ParentRequestId" IS NOT NULL THEN
                        PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."ParentRequestId" AND "TenantId"=NEW."TenantId" FOR KEY SHARE;
                        IF NOT FOUND OR NEW."ParentRequestId"=NEW."RequestId" THEN
                            RAISE EXCEPTION 'Request parent must already exist in its tenant' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NEW."RevisedFromRequestId" IS NOT NULL THEN
                        PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."RevisedFromRequestId" AND "TenantId"=NEW."TenantId" AND "Status"='REJECTED' FOR KEY SHARE;
                        IF NOT FOUND OR NEW."RevisedFromRequestId"=NEW."RequestId" THEN
                            RAISE EXCEPTION 'Revision source must be a rejected request in its tenant' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NEW."WorkflowVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
                        WHERE v."WorkflowVersionId"=NEW."WorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND w."BusinessType"='REQUEST' AND v."Status"='PUBLISHED') THEN
                        RAISE EXCEPTION 'Applied workflow must be a published request workflow in its tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."SLAVersionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
                        WHERE v."SLAVersionId"=NEW."SLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Applied SLA must belong to the request tenant' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER request_guard BEFORE INSERT OR UPDATE OR DELETE ON "Request" FOR EACH ROW EXECUTE FUNCTION bizflow_request_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Request") THEN
                        RAISE EXCEPTION 'Cannot discard request history' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TRIGGER request_guard ON "Request";
                DROP FUNCTION bizflow_request_guard();
                """);
            migrationBuilder.DropTable(
                name: "Request");

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
        }
    }
}
