using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestEscalationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestEscalation",
                columns: table => new
                {
                    RequestEscalationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestEscalation", x => x.RequestEscalationId);
                    table.CheckConstraint("CK_RequestEscalation_Reason", "length(btrim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_RequestEscalation_State", "\"State\" IN ('PENDING', 'APPROVED', 'REJECTED')");
                    table.ForeignKey(
                        name: "FK_RequestEscalation_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestEscalation_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestEscalation_User_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestEscalation_User_EscalatedById",
                        column: x => x.EscalatedById,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestEscalation_DecidedById",
                table: "RequestEscalation",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_RequestEscalation_EscalatedById",
                table: "RequestEscalation",
                column: "EscalatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RequestEscalation_RequestId",
                table: "RequestEscalation",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestEscalation_TenantId_RequestId_EscalatedAt",
                table: "RequestEscalation",
                columns: new[] { "TenantId", "RequestId", "EscalatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestEscalation_TenantId_State",
                table: "RequestEscalation",
                columns: new[] { "TenantId", "State" });

            // New permissions for the hierarchical approval flow.
            //   requests.escalate        -> a manager pushes a request up for higher approval (granted to MANAGER + COMPANY_ADMIN)
            //   requests.approve.elevated-> an elevated approver (e.g. a Director role) approves/rejects it (granted to COMPANY_ADMIN;
            //                               the operator grants this to their own "Giám đốc" role via the roles screen)
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a12000-0000-7000-8000-00000000000d','requests.escalate','requests','escalate','TENANT'),
                    ('01a12000-0000-7000-8000-00000000000e','requests.approve.elevated','requests','approve.elevated','TENANT')
                ON CONFLICT ("PermissionId") DO NOTHING;

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-00000000000d'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-00000000000d'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-00000000000e')
                ON CONFLICT ("RoleId","PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestEscalation");

            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "PermissionId" IN
                    ('01a12000-0000-7000-8000-00000000000d','01a12000-0000-7000-8000-00000000000e');
                DELETE FROM "Permission" WHERE "PermissionId" IN
                    ('01a12000-0000-7000-8000-00000000000d','01a12000-0000-7000-8000-00000000000e');
                """);
        }
    }
}
