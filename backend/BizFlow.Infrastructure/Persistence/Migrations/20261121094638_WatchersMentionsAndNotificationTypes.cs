using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WatchersMentionsAndNotificationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification");

            migrationBuilder.CreateTable(
                name: "CommentMention",
                columns: table => new
                {
                    CommentMentionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentMention", x => x.CommentMentionId);
                    table.CheckConstraint("CK_CommentMention_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
                    table.ForeignKey(
                        name: "FK_CommentMention_Comment_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comment",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentMention_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommentMention_User_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemWatcher",
                columns: table => new
                {
                    WorkItemWatcherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemWatcher", x => x.WorkItemWatcherId);
                    table.CheckConstraint("CK_WorkItemWatcher_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
                    table.ForeignKey(
                        name: "FK_WorkItemWatcher_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemWatcher_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification",
                sql: "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired','Mentioned','WatchedItemActivity')");

            migrationBuilder.CreateIndex(
                name: "IX_CommentMention_CommentId_MentionedUserId",
                table: "CommentMention",
                columns: new[] { "CommentId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentMention_MentionedUserId",
                table: "CommentMention",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentMention_TenantId_MentionedUserId_CreatedAt",
                table: "CommentMention",
                columns: new[] { "TenantId", "MentionedUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemWatcher_TenantId_ObjectType_ObjectId_UserId",
                table: "WorkItemWatcher",
                columns: new[] { "TenantId", "ObjectType", "ObjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemWatcher_TenantId_UserId",
                table: "WorkItemWatcher",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemWatcher_UserId",
                table: "WorkItemWatcher",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentMention");

            migrationBuilder.DropTable(
                name: "WorkItemWatcher");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notification_Type",
                table: "Notification",
                sql: "\"Type\" IN ('TaskAssigned','TaskAccepted','TaskRejected','ProgressSubmitted','ResultSubmitted','TaskConfirmed','RequestSubmitted','RequestRouted','RequestReceived','RequestResolved','RequestRejected','RequestRevised','DeadlineWarning','Overdue','Escalation','ApprovalRequired')");
        }
    }
}
