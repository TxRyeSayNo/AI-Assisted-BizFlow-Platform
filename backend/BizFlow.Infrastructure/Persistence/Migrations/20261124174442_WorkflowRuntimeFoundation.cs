using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRuntimeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormDataJson",
                table: "Request",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "WorkflowInstance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_User_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_WorkflowVersion_WorkflowVersionId",
                        column: x => x.WorkflowVersionId,
                        principalTable: "WorkflowVersion",
                        principalColumn: "WorkflowVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepExecution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Policy = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CandidatesJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepExecution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepExecution_WorkflowInstance_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "WorkflowInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDecision",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDecision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDecision_User_ActorId",
                        column: x => x.ActorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDecision_WorkflowStepExecution_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "WorkflowStepExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowParticipant",
                columns: table => new
                {
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowParticipant", x => new { x.StepExecutionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkflowParticipant_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowParticipant_WorkflowStepExecution_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "WorkflowStepExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDecision_ActorId",
                table: "WorkflowDecision",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDecision_StepExecutionId_ActorId",
                table: "WorkflowDecision",
                columns: new[] { "StepExecutionId", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_RequesterId",
                table: "WorkflowInstance",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_RequestId",
                table: "WorkflowInstance",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_TenantId_RequestId",
                table: "WorkflowInstance",
                columns: new[] { "TenantId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_WorkflowVersionId",
                table: "WorkflowInstance",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowParticipant_TenantId_UserId_InstanceId",
                table: "WorkflowParticipant",
                columns: new[] { "TenantId", "UserId", "InstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowParticipant_UserId",
                table: "WorkflowParticipant",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepExecution_InstanceId",
                table: "WorkflowStepExecution",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepExecution_TenantId_InstanceId_StartedAt",
                table: "WorkflowStepExecution",
                columns: new[] { "TenantId", "InstanceId", "StartedAt" });
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_runtime_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN
                    RAISE EXCEPTION 'Workflow execution history is retained' USING ERRCODE='23514';
                  END IF;
                  IF TG_TABLE_NAME='WorkflowInstance' THEN
                    IF NEW."Revision" < 1 OR NEW."Status" NOT IN ('RUNNING','COMPLETED','REJECTED','CANCELLED','FAILED') OR
                       (NEW."Status"='RUNNING') <> (NEW."CompletedAt" IS NULL) OR jsonb_typeof(NEW."PayloadJson") <> 'object' OR
                       NOT EXISTS (SELECT 1 FROM "Request" r JOIN "WorkflowVersion" v ON v."WorkflowVersionId"=NEW."WorkflowVersionId"
                         JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId" WHERE r."RequestId"=NEW."RequestId" AND
                         r."TenantId"=NEW."TenantId" AND r."RequesterId"=NEW."RequesterId" AND w."TenantId"=r."TenantId" AND w."BusinessType"='REQUEST') THEN
                      RAISE EXCEPTION 'Invalid execution ownership or state' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' AND ((to_jsonb(NEW)-ARRAY['Status','Revision','UpdatedAt','CompletedAt']) IS DISTINCT FROM
                        (to_jsonb(OLD)-ARRAY['Status','Revision','UpdatedAt','CompletedAt']) OR OLD."Status" <> 'RUNNING' OR NEW."Revision" <= OLD."Revision") THEN
                      RAISE EXCEPTION 'Workflow version and evidence are immutable' USING ERRCODE='23514';
                    END IF;
                  ELSIF TG_TABLE_NAME='WorkflowStepExecution' THEN
                    IF NOT EXISTS (SELECT 1 FROM "WorkflowInstance" i WHERE i."Id"=NEW."InstanceId" AND i."TenantId"=NEW."TenantId") THEN
                      RAISE EXCEPTION 'Step tenant mismatch' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' AND (OLD."Status" <> 'PENDING' OR NEW."Status" <> 'COMPLETED' OR
                      (to_jsonb(NEW)-ARRAY['Status','Outcome','CompletedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Outcome','CompletedAt'])) THEN
                      RAISE EXCEPTION 'Step snapshot is immutable' USING ERRCODE='23514';
                    END IF;
                  ELSIF TG_TABLE_NAME='WorkflowParticipant' THEN
                    IF TG_OP <> 'INSERT' OR NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s JOIN "User" u ON u."UserId"=NEW."UserId"
                      WHERE s."Id"=NEW."StepExecutionId" AND s."InstanceId"=NEW."InstanceId" AND s."TenantId"=NEW."TenantId" AND u."TenantId"=NEW."TenantId") THEN
                      RAISE EXCEPTION 'Invalid participant snapshot' USING ERRCODE='23514';
                    END IF;
                  ELSIF TG_TABLE_NAME='WorkflowDecision' THEN
                    IF TG_OP <> 'INSERT' OR NOT EXISTS (SELECT 1 FROM "WorkflowParticipant" p WHERE p."StepExecutionId"=NEW."StepExecutionId" AND
                      p."InstanceId"=NEW."InstanceId" AND p."TenantId"=NEW."TenantId" AND p."UserId"=NEW."ActorId") THEN
                      RAISE EXCEPTION 'Decision requires a participant in this tenant' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                DO $$ DECLARE t text; BEGIN
                  FOREACH t IN ARRAY ARRAY['WorkflowInstance','WorkflowStepExecution','WorkflowParticipant','WorkflowDecision'] LOOP
                    EXECUTE format('CREATE TRIGGER runtime_guard BEFORE INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION bizflow_runtime_guard()',t);
                    EXECUTE format('CREATE TRIGGER runtime_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION bizflow_runtime_guard()',t);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Request") OR EXISTS (SELECT 1 FROM "WorkflowInstance") THEN
                    RAISE EXCEPTION 'Request form data and workflow history must be retained' USING ERRCODE='23514';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "WorkflowDecision");

            migrationBuilder.DropTable(
                name: "WorkflowParticipant");

            migrationBuilder.DropTable(
                name: "WorkflowStepExecution");

            migrationBuilder.DropTable(
                name: "WorkflowInstance");
            migrationBuilder.Sql("DROP FUNCTION bizflow_runtime_guard();");

            migrationBuilder.DropColumn(
                name: "FormDataJson",
                table: "Request");
        }
    }
}
