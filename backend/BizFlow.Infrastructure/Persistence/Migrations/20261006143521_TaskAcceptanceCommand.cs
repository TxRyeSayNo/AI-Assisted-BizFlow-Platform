using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskAcceptanceCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Confirmation",
                columns: table => new
                {
                    ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "text", nullable: false, defaultValue: "TASK"),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneType = table.Column<string>(type: "text", nullable: false, defaultValue: "RESULT"),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false, defaultValue: "CONFIRMED"),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Confirmation", x => x.ConfirmationId);
                    table.CheckConstraint("CK_Confirmation_Decision", "\"Decision\" IN ('CONFIRMED','REJECTED')");
                    table.CheckConstraint("CK_Confirmation_Milestone", "\"MilestoneType\" IN ('RECEIVE','RESULT','RESOLUTION')");
                    table.CheckConstraint("CK_Confirmation_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
                    table.ForeignKey(
                        name: "FK_Confirmation_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Confirmation_User_ActorId",
                        column: x => x.ActorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Confirmation_ActorId",
                table: "Confirmation",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Confirmation_TenantId_ObjectType_ObjectId_ConfirmedAt",
                table: "Confirmation",
                columns: new[] { "TenantId", "ObjectType", "ObjectId", "ConfirmedAt" });
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId","Code","Module","Action","ScopeType") VALUES
                  ('01a11200-0000-7000-8000-000000000001','tasks.accept','tasks','accept','TENANT');
                INSERT INTO "RolePermission" ("RoleId","PermissionId") VALUES
                  ('019f7f8a-0000-7000-8000-000000000003','01a11200-0000-7000-8000-000000000001'),
                  ('019f7f8a-0000-7000-8000-000000000004','01a11200-0000-7000-8000-000000000001');
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_TaskAcceptanceReplay" CHECK (
                  "Action" <> 'TASK.ACCEPTED' OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' = 'TASK.ACCEPT', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));
                CREATE UNIQUE INDEX "UX_AuditLog_TaskAcceptanceReplay" ON "AuditLog"
                  ("TenantId","ActorId",("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action"='TASK.ACCEPTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                CREATE FUNCTION bizflow_confirmation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'Confirmation history is immutable' USING ERRCODE='23514';
                  END IF;
                  IF NEW."ObjectType"='TASK' THEN
                    PERFORM 1 FROM "Task" WHERE "TaskId"=NEW."ObjectId" AND "TenantId"=NEW."TenantId" AND "DeletedAt" IS NULL FOR UPDATE;
                  ELSE
                    PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."ObjectId" AND "TenantId"=NEW."TenantId" AND "DeletedAt" IS NULL FOR UPDATE;
                  END IF;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Confirmation target must belong to its tenant' USING ERRCODE='23514'; END IF;
                  PERFORM 1 FROM "User" WHERE "UserId"=NEW."ActorId" AND "TenantId"=NEW."TenantId" AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE;
                  IF NOT FOUND THEN RAISE EXCEPTION 'Confirmation actor must be active in its tenant' USING ERRCODE='23514'; END IF;
                  IF NEW."Note" IS NOT NULL AND translate(NEW."Note", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                    RAISE EXCEPTION 'Confirmation note must be plain text' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER confirmation_guard BEFORE INSERT OR UPDATE OR DELETE ON "Confirmation"
                  FOR EACH ROW EXECUTE FUNCTION bizflow_confirmation_guard();
                CREATE TRIGGER confirmation_truncate_guard BEFORE TRUNCATE ON "Confirmation"
                  FOR EACH STATEMENT EXECUTE FUNCTION bizflow_confirmation_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Confirmation") OR EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action"='TASK.ACCEPTED') OR
                    EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId"='01a11200-0000-7000-8000-000000000001' AND
                      "RoleId" NOT IN ('019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004')) THEN
                    RAISE EXCEPTION 'Cannot discard confirmation, replay evidence or configured grants' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP INDEX "UX_AuditLog_TaskAcceptanceReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT "CK_AuditLog_TaskAcceptanceReplay";
                DELETE FROM "RolePermission" WHERE "PermissionId"='01a11200-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId"='01a11200-0000-7000-8000-000000000001';
                """);
            migrationBuilder.DropTable(
                name: "Confirmation");
            migrationBuilder.Sql("DROP FUNCTION bizflow_confirmation_guard();");
        }
    }
}
