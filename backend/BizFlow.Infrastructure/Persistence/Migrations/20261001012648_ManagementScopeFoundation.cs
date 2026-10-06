using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagementScopeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagementScope",
                columns: table => new
                {
                    ManagementScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncludeDescendants = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementScope", x => x.ManagementScopeId);
                    table.ForeignKey(
                        name: "FK_ManagementScope_Department_TenantId_DepartmentId",
                        columns: x => new { x.TenantId, x.DepartmentId },
                        principalTable: "Department",
                        principalColumns: new[] { "TenantId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementScope_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementScope_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementScope_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementScope_CreatedBy",
                table: "ManagementScope",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementScope_TenantId_DepartmentId",
                table: "ManagementScope",
                columns: new[] { "TenantId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementScope_TenantId_UserId_DepartmentId",
                table: "ManagementScope",
                columns: new[] { "TenantId", "UserId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagementScope_UserId",
                table: "ManagementScope",
                column: "UserId");

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_management_scope_boundary() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND (
                        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
                        NEW."UserId" IS DISTINCT FROM OLD."UserId" OR
                        NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR
                        NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt") THEN
                        RAISE EXCEPTION 'Management scope ownership and attribution are immutable' USING ERRCODE = '23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId" = NEW."UserId" AND "TenantId" = NEW."TenantId")
                        OR NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId" = NEW."CreatedBy" AND "TenantId" = NEW."TenantId") THEN
                        RAISE EXCEPTION 'Management scope user and creator must belong to its tenant' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER management_scope_boundary BEFORE INSERT OR UPDATE ON "ManagementScope"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_management_scope_boundary();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not silently discard configured authorization boundaries on rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "ManagementScope") THEN
                        RAISE EXCEPTION 'Cannot roll back populated management scope configuration' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP TRIGGER management_scope_boundary ON "ManagementScope";
                DROP FUNCTION bizflow_management_scope_boundary();
                """);
            migrationBuilder.DropTable(
                name: "ManagementScope");
        }
    }
}
