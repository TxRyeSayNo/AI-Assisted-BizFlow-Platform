using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BizFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BizFlowDbContext))]
[Migration("20261125020000_WorkflowAgentRuns")]
public sealed class WorkflowAgentRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE "WorkflowAgentRun" (
          "Id" uuid PRIMARY KEY,
          "TenantId" uuid NOT NULL REFERENCES "Tenant"("TenantId") ON DELETE RESTRICT,
          "InstanceId" uuid NOT NULL REFERENCES "WorkflowInstance"("Id") ON DELETE RESTRICT,
          "RequestId" uuid NOT NULL REFERENCES "Request"("RequestId") ON DELETE RESTRICT,
          "StepId" uuid NOT NULL UNIQUE REFERENCES "WorkflowStepExecution"("Id") ON DELETE RESTRICT,
          "Mode" varchar(8) NOT NULL CHECK ("Mode" IN ('AUTO','REVIEW')),
          "Instructions" varchar(4000) NOT NULL,
          "InputJson" jsonb NOT NULL,
          "InputHash" varchar(64) NOT NULL,
          "Status" varchar(24) NOT NULL DEFAULT 'QUEUED' CHECK ("Status" IN ('QUEUED','RUNNING','RETRY','COMPLETED','REVIEW_REQUIRED','FAILED','SUPERSEDED','HUMAN_COMPLETED')),
          "Attempts" integer NOT NULL DEFAULT 0 CHECK ("Attempts" BETWEEN 0 AND 9),
          "CreatedAt" timestamptz NOT NULL,
          "NextAttemptAt" timestamptz NOT NULL,
          "LeaseToken" uuid NULL,
          "LeaseUntil" timestamptz NULL,
          "CompletedAt" timestamptz NULL,
          "ResultJson" jsonb NULL,
          "ErrorCode" varchar(100) NULL,
          "Model" varchar(200) NULL
        );
        CREATE INDEX ON "WorkflowAgentRun" ("Status","NextAttemptAt");
        CREATE INDEX ON "WorkflowAgentRun" ("TenantId","InstanceId");
        CREATE TABLE "WorkflowAgentAttempt" (
          "RunId" uuid NOT NULL REFERENCES "WorkflowAgentRun"("Id") ON DELETE RESTRICT,
          "Attempt" integer NOT NULL CHECK ("Attempt" BETWEEN 1 AND 9),
          "TenantId" uuid NOT NULL REFERENCES "Tenant"("TenantId") ON DELETE RESTRICT,
          "StartedAt" timestamptz NOT NULL,
          "CompletedAt" timestamptz NOT NULL,
          "Model" varchar(200) NULL,
          "ErrorCode" varchar(100) NULL,
          "TraceJson" jsonb NOT NULL,
          PRIMARY KEY ("RunId","Attempt")
        );
        CREATE FUNCTION bizflow_workflow_agent_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Agent evidence cannot be deleted' USING ERRCODE='23514'; END IF;
          IF NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s JOIN "WorkflowInstance" i ON i."Id"=s."InstanceId"
            WHERE s."Id"=NEW."StepId" AND s."TenantId"=NEW."TenantId" AND s."InstanceId"=NEW."InstanceId"
              AND i."RequestId"=NEW."RequestId" AND i."TenantId"=NEW."TenantId" AND s."NodeType"='DEPARTMENT') THEN
            RAISE EXCEPTION 'Invalid agent run ownership' USING ERRCODE='23514';
          END IF;
          IF TG_OP='UPDATE' AND (NEW."Id",NEW."TenantId",NEW."InstanceId",NEW."StepId",NEW."RequestId",NEW."Mode",NEW."Instructions",NEW."InputJson",NEW."InputHash",NEW."CreatedAt")
            IS DISTINCT FROM (OLD."Id",OLD."TenantId",OLD."InstanceId",OLD."StepId",OLD."RequestId",OLD."Mode",OLD."Instructions",OLD."InputJson",OLD."InputHash",OLD."CreatedAt") THEN
            RAISE EXCEPTION 'Agent input is immutable' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER workflow_agent_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowAgentRun" FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_agent_guard();
        CREATE FUNCTION bizflow_workflow_agent_attempt_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Agent attempts are append only' USING ERRCODE='23514'; END IF;
          IF NOT EXISTS (SELECT 1 FROM "WorkflowAgentRun" WHERE "Id"=NEW."RunId" AND "TenantId"=NEW."TenantId") THEN
            RAISE EXCEPTION 'Invalid agent attempt ownership' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER workflow_agent_attempt_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowAgentAttempt" FOR EACH ROW EXECUTE FUNCTION bizflow_workflow_agent_attempt_guard();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE "WorkflowAgentAttempt";
        DROP TABLE "WorkflowAgentRun";
        DROP FUNCTION bizflow_workflow_agent_attempt_guard();
        DROP FUNCTION bizflow_workflow_agent_guard();
        """);
}
