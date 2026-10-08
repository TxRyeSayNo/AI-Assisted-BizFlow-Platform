using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskExecutionCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId","Code","Module","Action","ScopeType") VALUES
                  ('01a11300-0000-7000-8000-000000000001','tasks.execute','tasks','execute','TENANT');
                INSERT INTO "RolePermission" ("RoleId","PermissionId") VALUES
                  ('019f7f8a-0000-7000-8000-000000000003','01a11300-0000-7000-8000-000000000001'),
                  ('019f7f8a-0000-7000-8000-000000000004','01a11300-0000-7000-8000-000000000001');
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_TaskExecutionReplay" CHECK (
                  "Action" <> 'TASK.STARTED' OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' = 'TASK.START', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));
                CREATE UNIQUE INDEX "UX_AuditLog_TaskExecutionReplay" ON "AuditLog"
                  ("TenantId","ActorId",("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action"='TASK.STARTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action"='TASK.STARTED') OR
                    EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId"='01a11300-0000-7000-8000-000000000001' AND
                      "RoleId" NOT IN ('019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004')) THEN
                    RAISE EXCEPTION 'Cannot discard execution replay evidence or configured grants' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP INDEX "UX_AuditLog_TaskExecutionReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT "CK_AuditLog_TaskExecutionReplay";
                DELETE FROM "RolePermission" WHERE "PermissionId"='01a11300-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId"='01a11300-0000-7000-8000-000000000001';
                """);
        }
    }
}
