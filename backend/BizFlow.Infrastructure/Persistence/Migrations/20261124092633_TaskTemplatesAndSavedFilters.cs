using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskTemplatesAndSavedFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedFilter",
                columns: table => new
                {
                    SavedFilterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilter", x => x.SavedFilterId);
                    table.CheckConstraint("CK_SavedFilter_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_SavedFilter_Scope", "\"Scope\" IN ('TASK','REQUEST')");
                    table.ForeignKey(
                        name: "FK_SavedFilter_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedFilter_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplate",
                columns: table => new
                {
                    TaskTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TitleTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DeadlineOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceFrequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RecurrenceDay = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceHourUtc = table.Column<int>(type: "integer", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplate", x => x.TaskTemplateId);
                    table.CheckConstraint("CK_TaskTemplate_DeadlineOffset", "\"DeadlineOffsetDays\" IS NULL OR \"DeadlineOffsetDays\" BETWEEN 0 AND 365");
                    table.CheckConstraint("CK_TaskTemplate_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_TaskTemplate_Recurrence", "\"RecurrenceFrequency\" IS NULL OR \"RecurrenceFrequency\" IN ('DAILY','WEEKLY','MONTHLY')");
                    table.CheckConstraint("CK_TaskTemplate_RecurrenceShape", "(\"RecurrenceFrequency\" IS NULL AND \"RecurrenceDay\" IS NULL AND \"RecurrenceHourUtc\" IS NULL\n   AND \"NextRunAt\" IS NULL)\nOR (\"RecurrenceFrequency\" = 'DAILY' AND \"RecurrenceDay\" IS NULL\n   AND \"RecurrenceHourUtc\" BETWEEN 0 AND 23)\nOR (\"RecurrenceFrequency\" = 'WEEKLY' AND \"RecurrenceDay\" BETWEEN 1 AND 7\n   AND \"RecurrenceHourUtc\" BETWEEN 0 AND 23)\nOR (\"RecurrenceFrequency\" = 'MONTHLY' AND \"RecurrenceDay\" BETWEEN 1 AND 28\n   AND \"RecurrenceHourUtc\" BETWEEN 0 AND 23)");
                    table.ForeignKey(
                        name: "FK_TaskTemplate_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTemplate_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilter_TenantId_UserId_Scope_NormalizedName",
                table: "SavedFilter",
                columns: new[] { "TenantId", "UserId", "Scope", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilter_UserId",
                table: "SavedFilter",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplate_CreatedBy",
                table: "TaskTemplate",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplate_NextRunAt",
                table: "TaskTemplate",
                column: "NextRunAt",
                filter: "\"NextRunAt\" IS NOT NULL AND \"ArchivedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplate_TenantId_NormalizedName",
                table: "TaskTemplate",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedFilter");

            migrationBuilder.DropTable(
                name: "TaskTemplate");
        }
    }
}
