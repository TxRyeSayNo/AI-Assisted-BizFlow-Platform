using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskCreationReplayProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_TaskReplay" CHECK (
                  "Action" <> 'TASK.CREATED' OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' = 'TASK.CREATE', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));
                CREATE UNIQUE INDEX "UX_AuditLog_TaskCreationReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'TASK.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action"='TASK.CREATED' AND "MetadataJson" ? 'idempotency') THEN
                    RAISE EXCEPTION 'Task replay evidence exists; replay protection cannot be removed.';
                  END IF;
                END $$;
                DROP INDEX "UX_AuditLog_TaskCreationReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT "CK_AuditLog_TaskReplay";
                """);

        }
    }
}
