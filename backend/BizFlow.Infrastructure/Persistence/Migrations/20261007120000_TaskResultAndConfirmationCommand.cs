using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskResultAndConfirmationCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId","Code","Module","Action","ScopeType") VALUES
                  ('01a11400-0000-7000-8000-000000000001','tasks.submit','tasks','submit','TENANT'),
                  ('01a11500-0000-7000-8000-000000000001','tasks.confirm','tasks','confirm','TENANT');
                INSERT INTO "RolePermission" ("RoleId","PermissionId") VALUES
                  ('019f7f8a-0000-7000-8000-000000000003','01a11400-0000-7000-8000-000000000001'),
                  ('019f7f8a-0000-7000-8000-000000000004','01a11400-0000-7000-8000-000000000001'),
                  ('019f7f8a-0000-7000-8000-000000000002','01a11500-0000-7000-8000-000000000001'),
                  ('019f7f8a-0000-7000-8000-000000000003','01a11500-0000-7000-8000-000000000001');
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_TaskResultSubmissionReplay" CHECK (
                  "Action" <> 'TASK.RESULT_SUBMITTED' OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' = 'TASK.RESULT_SUBMIT', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));
                CREATE UNIQUE INDEX "UX_AuditLog_TaskResultSubmissionReplay" ON "AuditLog"
                  ("TenantId","ActorId",("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action"='TASK.RESULT_SUBMITTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_TaskResultConfirmationReplay" CHECK (
                  "Action" NOT IN ('TASK.RESULT_CONFIRMED', 'TASK.RESULT_REWORK') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('TASK.RESULT_CONFIRM', 'TASK.RESULT_REWORK'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));
                CREATE UNIQUE INDEX "UX_AuditLog_TaskResultConfirmationReplay" ON "AuditLog"
                  ("TenantId","ActorId",("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" IN ('TASK.RESULT_CONFIRMED', 'TASK.RESULT_REWORK') AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                CREATE OR REPLACE FUNCTION bizflow_task_evidence_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz; parent_xmin xid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Task evidence is append-only' USING ERRCODE='23514';
                    END IF;
                    SELECT "TenantId", "Status", "DeletedAt", xmin INTO parent_tenant, parent_status, parent_deleted, parent_xmin
                    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
                    IF NOT FOUND OR parent_deleted IS NOT NULL THEN
                        RAISE EXCEPTION 'Evidence requires a live task' USING ERRCODE='23514';
                    END IF;
                    -- ADR-0003: overdue work must resume before result submission.
                    IF TG_TABLE_NAME='TaskResult' AND parent_status <> 'IN_PROGRESS' THEN
                        IF parent_status <> 'SUBMITTED' OR parent_xmin::text <> pg_current_xact_id()::text::xid::text THEN
                            RAISE EXCEPTION 'Result submission requires in-progress work' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."AuthorId" AND "TenantId"=parent_tenant) THEN
                        RAISE EXCEPTION 'Evidence author must belong to the task tenant' USING ERRCODE='23514';
                    END IF;
                    IF translate(NEW."Content", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Evidence text contains unsupported control characters' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action" IN ('TASK.RESULT_SUBMITTED', 'TASK.RESULT_CONFIRMED', 'TASK.RESULT_REWORK')) OR
                    EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId" IN ('01a11400-0000-7000-8000-000000000001','01a11500-0000-7000-8000-000000000001') AND
                      "RoleId" NOT IN ('019f7f8a-0000-7000-8000-000000000002','019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004')) THEN
                    RAISE EXCEPTION 'Cannot discard result submission/confirmation replay evidence or configured grants' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP INDEX "UX_AuditLog_TaskResultConfirmationReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT "CK_AuditLog_TaskResultConfirmationReplay";
                DROP INDEX "UX_AuditLog_TaskResultSubmissionReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT "CK_AuditLog_TaskResultSubmissionReplay";
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a11400-0000-7000-8000-000000000001','01a11500-0000-7000-8000-000000000001');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a11400-0000-7000-8000-000000000001','01a11500-0000-7000-8000-000000000001');
                """);
        }
    }
}
