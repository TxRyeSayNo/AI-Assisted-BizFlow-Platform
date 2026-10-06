using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.NotificationId);
                    table.CheckConstraint("CK_Notification_Text", "length(btrim(\"Title\")) > 0 AND length(btrim(\"Content\")) > 0 AND length(btrim(\"IdempotencyKey\")) > 0");
                    table.CheckConstraint("CK_Notification_Type", "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired')");
                    table.ForeignKey(
                        name: "FK_Notification_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_User_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientId",
                table: "Notification",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TenantId_IdempotencyKey",
                table: "Notification",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TenantId_RecipientId_NotificationId",
                table: "Notification",
                columns: new[] { "TenantId", "RecipientId", "NotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TenantId_RecipientId_ReadAt",
                table: "Notification",
                columns: new[] { "TenantId", "RecipientId", "ReadAt" });
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_notification_boundary() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."RecipientId" AND "TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Notification recipient must belong to its tenant' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' THEN
                        IF (to_jsonb(NEW) - 'ReadAt' - 'SentAt') IS DISTINCT FROM (to_jsonb(OLD) - 'ReadAt' - 'SentAt') THEN
                            RAISE EXCEPTION 'Notification identity and event payload are immutable' USING ERRCODE='23514';
                        END IF;
                        IF (OLD."ReadAt" IS NOT NULL AND NEW."ReadAt" IS DISTINCT FROM OLD."ReadAt") OR
                           (OLD."SentAt" IS NOT NULL AND NEW."SentAt" IS DISTINCT FROM OLD."SentAt") THEN
                            RAISE EXCEPTION 'Notification receipt timestamps cannot be overwritten' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER notification_boundary BEFORE INSERT OR UPDATE ON "Notification"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_notification_boundary();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Notification") THEN
                        RAISE EXCEPTION 'Cannot roll back populated notification history' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TRIGGER notification_boundary ON "Notification";
                DROP FUNCTION bizflow_notification_boundary();
                """);
            migrationBuilder.DropTable(
                name: "Notification");
        }
    }
}
