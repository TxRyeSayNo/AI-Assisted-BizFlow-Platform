using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comment",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comment", x => x.CommentId);
                    table.CheckConstraint("CK_Comment_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST','RESULT','PROGRESS')");
                    table.ForeignKey(
                        name: "FK_Comment_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comment_User_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_AuthorId",
                table: "Comment",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_TenantId_ObjectType_ObjectId_CreatedAt",
                table: "Comment",
                columns: new[] { "TenantId", "ObjectType", "ObjectId", "CreatedAt" });

            migrationBuilder.Sql("""
                -- Trigger function to enforce multi-tenant and cross-object integrity on Comment
                CREATE OR REPLACE FUNCTION bizflow_comment_guard() RETURNS trigger AS $$
                DECLARE
                    v_author_tenant uuid;
                    v_target_tenant uuid;
                BEGIN
                    -- Validate author belongs to the same tenant
                    SELECT "TenantId" INTO v_author_tenant FROM "User" WHERE "UserId" = NEW."AuthorId";
                    IF v_author_tenant IS NULL OR v_author_tenant <> NEW."TenantId" THEN
                        RAISE EXCEPTION 'Comment author must belong to the comment tenant.' USING ERRCODE = '23503';
                    END IF;

                    -- Validate target object belongs to the same tenant
                    IF NEW."ObjectType" = 'TASK' THEN
                        SELECT "TenantId" INTO v_target_tenant FROM "Task" WHERE "TaskId" = NEW."ObjectId";
                        IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
                            RAISE EXCEPTION 'Target Task must exist in the same tenant.' USING ERRCODE = '23503';
                        END IF;
                    ELSIF NEW."ObjectType" = 'REQUEST' THEN
                        SELECT "TenantId" INTO v_target_tenant FROM "Request" WHERE "RequestId" = NEW."ObjectId";
                        IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
                            RAISE EXCEPTION 'Target Request must exist in the same tenant.' USING ERRCODE = '23503';
                        END IF;
                    END IF;

                    -- Immutability on update
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD."TenantId" <> NEW."TenantId" THEN
                            RAISE EXCEPTION 'Comment TenantId is immutable.' USING ERRCODE = '23514';
                        END IF;
                        IF OLD."ObjectType" <> NEW."ObjectType" THEN
                            RAISE EXCEPTION 'Comment ObjectType is immutable.' USING ERRCODE = '23514';
                        END IF;
                        IF OLD."ObjectId" <> NEW."ObjectId" THEN
                            RAISE EXCEPTION 'Comment ObjectId is immutable.' USING ERRCODE = '23514';
                        END IF;
                        IF OLD."AuthorId" <> NEW."AuthorId" THEN
                            RAISE EXCEPTION 'Comment AuthorId is immutable.' USING ERRCODE = '23514';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_bizflow_comment_guard ON "Comment";
                CREATE TRIGGER trg_bizflow_comment_guard
                    BEFORE INSERT OR UPDATE ON "Comment"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_comment_guard();

                -- Permissions
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a14000-0000-7000-8000-000000000001','comments.create','collaboration','create','TENANT'),
                    ('01a14000-0000-7000-8000-000000000002','comments.read','collaboration','read','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000002');

                -- Idempotency Replay Index for Comment Creation
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_AuditLog_CommentCreationReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'COMMENT.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_bizflow_comment_guard ON "Comment";
                DROP FUNCTION IF EXISTS bizflow_comment_guard();
                DROP INDEX IF EXISTS "UX_AuditLog_CommentCreationReplay";
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000001', '01a14000-0000-7000-8000-000000000002');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000001', '01a14000-0000-7000-8000-000000000002');
                """);

            migrationBuilder.DropTable(
                name: "Comment");
        }
    }
}
