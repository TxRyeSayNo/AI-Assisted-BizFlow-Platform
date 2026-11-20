using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskParentTaskAndAiLearnedChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                table: "Task",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiLearnedChoice",
                columns: table => new
                {
                    ChoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SemanticKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ChoiceValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChoiceLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PERSONAL"),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiLearnedChoice", x => x.ChoiceId);
                    table.ForeignKey(
                        name: "FK_AiLearnedChoice_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiLearnedChoice_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Task_ParentTaskId",
                table: "Task",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AiLearnedChoice_TenantId_SemanticKey_UsageCount",
                table: "AiLearnedChoice",
                columns: new[] { "TenantId", "SemanticKey", "UsageCount" });

            migrationBuilder.CreateIndex(
                name: "IX_AiLearnedChoice_TenantId_UserId_SemanticKey",
                table: "AiLearnedChoice",
                columns: new[] { "TenantId", "UserId", "SemanticKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AiLearnedChoice_UserId",
                table: "AiLearnedChoice",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Task_Task_ParentTaskId",
                table: "Task",
                column: "ParentTaskId",
                principalTable: "Task",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Task") THEN
                        RAISE EXCEPTION 'Cannot discard task history during rollback' USING ERRCODE='23514';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Task_Task_ParentTaskId",
                table: "Task");

            migrationBuilder.DropTable(
                name: "AiLearnedChoice");

            migrationBuilder.DropIndex(
                name: "IX_Task_ParentTaskId",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "Task");
        }
    }
}
