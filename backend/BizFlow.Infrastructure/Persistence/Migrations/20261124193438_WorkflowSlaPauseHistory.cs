using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowSlaPauseHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAt",
                table: "WorkflowStepExecution",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowSlaPause",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PausedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PausedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAtBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CalendarSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResumedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAtAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSlaPause", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSlaPause_User_PausedBy",
                        column: x => x.PausedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSlaPause_User_ResumedBy",
                        column: x => x.ResumedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSlaPause_WorkflowStepExecution_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "WorkflowStepExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSlaPause_PausedBy",
                table: "WorkflowSlaPause",
                column: "PausedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSlaPause_ResumedBy",
                table: "WorkflowSlaPause",
                column: "ResumedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSlaPause_StepExecutionId",
                table: "WorkflowSlaPause",
                column: "StepExecutionId",
                unique: true,
                filter: "\"ResumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSlaPause_TenantId_InstanceId_PausedAt",
                table: "WorkflowSlaPause",
                columns: new[] { "TenantId", "InstanceId", "PausedAt" });
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_step_sla_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Step history is retained' USING ERRCODE='23514'; END IF;
                  IF NOT EXISTS (SELECT 1 FROM "WorkflowInstance" i WHERE i."Id"=NEW."InstanceId" AND i."TenantId"=NEW."TenantId") THEN
                    RAISE EXCEPTION 'Step tenant mismatch' USING ERRCODE='23514';
                  END IF;
                  IF TG_OP='UPDATE' THEN
                    IF OLD."Status"<>'PENDING' OR NOT COALESCE((
                      (NEW."Status"='COMPLETED' AND OLD."PausedAt" IS NULL AND NEW."PausedAt" IS NULL AND
                       (to_jsonb(NEW)-ARRAY['Status','Outcome','CompletedAt']) IS NOT DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Outcome','CompletedAt'])) OR
                      (NEW."Status"='PENDING' AND OLD."DueAt" IS NOT NULL AND OLD."PausedAt" IS NULL AND NEW."PausedAt">=OLD."StartedAt" AND
                       (to_jsonb(NEW)-'PausedAt') IS NOT DISTINCT FROM (to_jsonb(OLD)-'PausedAt')) OR
                      (NEW."Status"='PENDING' AND OLD."PausedAt" IS NOT NULL AND NEW."PausedAt" IS NULL AND NEW."DueAt">=OLD."DueAt" AND
                       (to_jsonb(NEW)-ARRAY['PausedAt','DueAt']) IS NOT DISTINCT FROM (to_jsonb(OLD)-ARRAY['PausedAt','DueAt']))
                    ),false) THEN RAISE EXCEPTION 'Step snapshot or SLA transition is invalid' USING ERRCODE='23514'; END IF;
                  ELSIF NEW."PausedAt" IS NOT NULL THEN RAISE EXCEPTION 'A new step cannot start paused' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                DROP TRIGGER runtime_guard ON "WorkflowStepExecution";
                CREATE TRIGGER runtime_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowStepExecution" FOR EACH ROW EXECUTE FUNCTION bizflow_step_sla_guard();

                CREATE FUNCTION bizflow_sla_pause_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'SLA pause evidence is retained' USING ERRCODE='23514'; END IF;
                  IF TG_TABLE_NAME='WorkflowDecision' THEN
                    IF EXISTS (SELECT 1 FROM "WorkflowStepExecution" WHERE "Id"=NEW."StepExecutionId" AND "PausedAt" IS NOT NULL) THEN
                      RAISE EXCEPTION 'Resume SLA before deciding a paused step' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                  END IF;
                  IF NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s JOIN "User" u ON u."UserId"=NEW."PausedBy"
                    WHERE s."Id"=NEW."StepExecutionId" AND s."InstanceId"=NEW."InstanceId" AND s."TenantId"=NEW."TenantId" AND
                    u."TenantId"=NEW."TenantId" AND NEW."PausedAt">=s."StartedAt") OR
                    (NEW."CalendarSnapshotJson" IS NOT NULL AND jsonb_typeof(NEW."CalendarSnapshotJson")<>'object') THEN
                    RAISE EXCEPTION 'Invalid SLA pause ownership or calendar snapshot' USING ERRCODE='23514';
                  END IF;
                  IF TG_OP='UPDATE' THEN
                    IF OLD."ResumedAt" IS NOT NULL OR NEW."ResumedAt" IS NULL OR NEW."ResumedAt"<OLD."PausedAt" OR NEW."DueAtAfter" IS NULL OR
                      NEW."DueAtAfter"<OLD."DueAtBefore" OR NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."ResumedBy" AND "TenantId"=NEW."TenantId") OR
                      (to_jsonb(NEW)-ARRAY['ResumedAt','ResumedBy','DueAtAfter']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['ResumedAt','ResumedBy','DueAtAfter']) THEN
                      RAISE EXCEPTION 'SLA evidence is immutable except its first resume' USING ERRCODE='23514';
                    END IF;
                  ELSIF NEW."ResumedAt" IS NOT NULL OR NEW."ResumedBy" IS NOT NULL OR NEW."DueAtAfter" IS NOT NULL OR btrim(NEW."Reason")='' THEN
                    RAISE EXCEPTION 'Invalid initial SLA pause' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER sla_pause_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowSlaPause" FOR EACH ROW EXECUTE FUNCTION bizflow_sla_pause_guard();
                CREATE TRIGGER sla_pause_truncate BEFORE TRUNCATE ON "WorkflowSlaPause" FOR EACH STATEMENT EXECUTE FUNCTION bizflow_sla_pause_guard();
                CREATE TRIGGER decision_sla_guard BEFORE INSERT ON "WorkflowDecision" FOR EACH ROW EXECUTE FUNCTION bizflow_sla_pause_guard();
                CREATE FUNCTION bizflow_sla_consistency_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME='WorkflowStepExecution' THEN
                    IF NEW."PausedAt" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "WorkflowSlaPause" p WHERE p."StepExecutionId"=NEW."Id" AND
                      p."PausedAt"=NEW."PausedAt" AND p."DueAtBefore"=NEW."DueAt" AND p."ResumedAt" IS NULL) THEN
                      RAISE EXCEPTION 'A paused step requires its durable pause evidence' USING ERRCODE='23514';
                    END IF;
                    IF OLD."PausedAt" IS NOT NULL AND NEW."PausedAt" IS NULL AND NOT EXISTS (SELECT 1 FROM "WorkflowSlaPause" p WHERE p."StepExecutionId"=NEW."Id" AND
                      p."PausedAt"=OLD."PausedAt" AND p."DueAtAfter"=NEW."DueAt" AND p."ResumedAt" IS NOT NULL) THEN
                      RAISE EXCEPTION 'A resumed deadline requires its durable resume evidence' USING ERRCODE='23514';
                    END IF;
                  ELSIF NEW."ResumedAt" IS NULL AND NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s WHERE s."Id"=NEW."StepExecutionId" AND
                    s."PausedAt"=NEW."PausedAt" AND s."DueAt"=NEW."DueAtBefore") THEN
                    RAISE EXCEPTION 'Open SLA pause must match the step state' USING ERRCODE='23514';
                  ELSIF NEW."ResumedAt" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s WHERE s."Id"=NEW."StepExecutionId" AND
                    s."PausedAt" IS NULL AND s."DueAt"=NEW."DueAtAfter") THEN
                    RAISE EXCEPTION 'Resume evidence must match the resumed step' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER sla_step_evidence AFTER UPDATE ON "WorkflowStepExecution" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bizflow_sla_consistency_guard();
                CREATE CONSTRAINT TRIGGER sla_pause_evidence AFTER INSERT OR UPDATE ON "WorkflowSlaPause" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bizflow_sla_consistency_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "WorkflowSlaPause") OR EXISTS (SELECT 1 FROM "WorkflowStepExecution" WHERE "PausedAt" IS NOT NULL) THEN
                    RAISE EXCEPTION 'SLA pause history cannot be discarded' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP TRIGGER decision_sla_guard ON "WorkflowDecision";
                DROP TRIGGER sla_step_evidence ON "WorkflowStepExecution";
                DROP TRIGGER sla_pause_evidence ON "WorkflowSlaPause";
                DROP FUNCTION bizflow_sla_consistency_guard();
                DROP TRIGGER runtime_guard ON "WorkflowStepExecution";
                CREATE TRIGGER runtime_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowStepExecution" FOR EACH ROW EXECUTE FUNCTION bizflow_runtime_guard();
                DROP FUNCTION bizflow_step_sla_guard();
                DROP TRIGGER sla_pause_guard ON "WorkflowSlaPause";
                DROP TRIGGER sla_pause_truncate ON "WorkflowSlaPause";
                DROP FUNCTION bizflow_sla_pause_guard();
                """);
            migrationBuilder.DropTable(
                name: "WorkflowSlaPause");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "WorkflowStepExecution");
        }
    }
}
