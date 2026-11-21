using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabelCatalogAndWorkItemLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Label",
                columns: table => new
                {
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Label", x => x.LabelId);
                    table.CheckConstraint("CK_Label_Colour", "\"Colour\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_Label_Name", "length(btrim(\"Name\")) > 0");
                    table.ForeignKey(
                        name: "FK_Label_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Label_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLabel",
                columns: table => new
                {
                    WorkItemLabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLabel", x => x.WorkItemLabelId);
                    table.CheckConstraint("CK_WorkItemLabel_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
                    table.ForeignKey(
                        name: "FK_WorkItemLabel_Label_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Label",
                        principalColumn: "LabelId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemLabel_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemLabel_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Label_CreatedBy",
                table: "Label",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Label_TenantId_NormalizedName",
                table: "Label",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabel_CreatedBy",
                table: "WorkItemLabel",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabel_LabelId",
                table: "WorkItemLabel",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabel_TenantId_LabelId_RemovedAt",
                table: "WorkItemLabel",
                columns: new[] { "TenantId", "LabelId", "RemovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabel_TenantId_ObjectType_ObjectId_LabelId",
                table: "WorkItemLabel",
                columns: new[] { "TenantId", "ObjectType", "ObjectId", "LabelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemLabel");

            migrationBuilder.DropTable(
                name: "Label");
        }
    }
}
