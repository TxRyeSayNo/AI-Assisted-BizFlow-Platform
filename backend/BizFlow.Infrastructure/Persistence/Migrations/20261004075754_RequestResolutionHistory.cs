using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestResolutionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestResolution",
                columns: table => new
                {
                    ResolutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    RevisionNo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestResolution", x => x.ResolutionId);
                    table.CheckConstraint("CK_RequestResolution_Content", "length(btrim(\"Content\")) > 0");
                    table.CheckConstraint("CK_RequestResolution_Revision", "\"RevisionNo\" > 0");
                    table.ForeignKey(
                        name: "FK_RequestResolution_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestResolution_User_ResolverId",
                        column: x => x.ResolverId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestResolution_RequestId_RevisionNo",
                table: "RequestResolution",
                columns: new[] { "RequestId", "RevisionNo" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestResolution_ResolverId",
                table: "RequestResolution",
                column: "ResolverId");

            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_request_resolution_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_tenant uuid; parent_status request_state; parent_deleted timestamptz;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Resolution history is append-only' USING ERRCODE='23514';
                    END IF;
                    -- Serialize evidence insertion with lifecycle changes; reject stale snapshots.
                    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
                    FROM "Request" WHERE "RequestId"=NEW."RequestId" FOR UPDATE;
                    IF NOT FOUND OR parent_status <> 'IN_PROGRESS' OR parent_deleted IS NOT NULL THEN
                        RAISE EXCEPTION 'Resolution requires an in-progress request' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."ResolverId" AND "TenantId"=parent_tenant) THEN
                        RAISE EXCEPTION 'Resolver must belong to the request tenant' USING ERRCODE='23514';
                    END IF;
                    IF NEW."Content" !~ '[^[:space:]]' OR translate(NEW."Content", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
                        RAISE EXCEPTION 'Resolution requires plain-text content' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER request_resolution_guard BEFORE INSERT OR UPDATE OR DELETE ON "RequestResolution"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_request_resolution_guard();
                CREATE TRIGGER request_resolution_truncate_guard BEFORE TRUNCATE ON "RequestResolution"
                    FOR EACH STATEMENT EXECUTE FUNCTION bizflow_request_resolution_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RequestResolution") THEN
                        RAISE EXCEPTION 'Cannot discard resolution history during rollback' USING ERRCODE='23514';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "RequestResolution");
            migrationBuilder.Sql("DROP FUNCTION bizflow_request_resolution_guard();");
        }
    }
}
