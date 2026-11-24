using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType")
                VALUES ('01a24000-0000-7000-8000-000000000001', 'notifications.send', 'notifications', 'send', 'TENANT');
                INSERT INTO "RolePermission" ("RoleId", "PermissionId")
                VALUES ('019f7f8a-0000-7000-8000-000000000002', '01a24000-0000-7000-8000-000000000001');
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification",
                sql: "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired','Mentioned','WatchedItemActivity','Announcement','MeetingInvitation')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RolePermission" WHERE "PermissionId"='01a24000-0000-7000-8000-000000000001'
                        AND "RoleId"<>'019f7f8a-0000-7000-8000-000000000002') THEN
                        RAISE EXCEPTION 'Cannot remove configured notification sending grants';
                    END IF;
                END $$;
                DELETE FROM "RolePermission" WHERE "PermissionId"='01a24000-0000-7000-8000-000000000001';
                DELETE FROM "Permission" WHERE "PermissionId"='01a24000-0000-7000-8000-000000000001';
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification",
                sql: "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired','Mentioned','WatchedItemActivity')");
        }
    }
}
