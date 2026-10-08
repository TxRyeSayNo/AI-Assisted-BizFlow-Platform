using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attachment",
                columns: table => new
                {
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachment", x => x.AttachmentId);
                    table.CheckConstraint("CK_Attachment_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST','COMMENT','RESULT','PROGRESS')");
                    table.CheckConstraint("CK_Attachment_Status", "\"Status\" IN ('UPLOADING','READY','FAILED','DELETED')");
                    table.CheckConstraint("CK_Attachment_SizeBytes", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 524288000");
                    table.ForeignKey(
                        name: "FK_Attachment_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachment_User_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_UploadedBy",
                table: "Attachment",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_TenantId_ObjectKey",
                table: "Attachment",
                columns: new[] { "TenantId", "ObjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_TenantId_ObjectType_ObjectId_CreatedAt",
                table: "Attachment",
                columns: new[] { "TenantId", "ObjectType", "ObjectId", "CreatedAt" });

            migrationBuilder.Sql("""
                -- Trigger function to enforce multi-tenant and cross-object integrity on Attachment
                CREATE OR REPLACE FUNCTION bizflow_attachment_guard() RETURNS trigger AS $$
                DECLARE
                    v_uploader_tenant uuid;
                    v_target_tenant uuid;
                BEGIN
                    -- Check uploader belongs to same tenant
                    SELECT "TenantId" INTO v_uploader_tenant FROM "User" WHERE "UserId" = NEW."UploadedBy" AND "DeletedAt" IS NULL;
                    IF v_uploader_tenant IS NULL OR v_uploader_tenant <> NEW."TenantId" THEN
                        RAISE EXCEPTION 'ATTACHMENT.INVALID_UPLOADER: Uploader must belong to the exact same tenant.' USING ERRCODE = '23503';
                    END IF;

                    -- Check target exists in same tenant
                    IF NEW."ObjectType" = 'TASK' THEN
                        SELECT "TenantId" INTO v_target_tenant FROM "Task" WHERE "TaskId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
                    ELSIF NEW."ObjectType" = 'REQUEST' THEN
                        SELECT "TenantId" INTO v_target_tenant FROM "Request" WHERE "RequestId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
                    ELSIF NEW."ObjectType" = 'COMMENT' THEN
                        SELECT "TenantId" INTO v_target_tenant FROM "Comment" WHERE "CommentId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
                    ELSIF NEW."ObjectType" = 'RESULT' THEN
                        SELECT t."TenantId" INTO v_target_tenant FROM "TaskResult" r JOIN "Task" t ON t."TaskId" = r."TaskId" WHERE r."TaskResultId" = NEW."ObjectId" AND t."DeletedAt" IS NULL;
                    ELSIF NEW."ObjectType" = 'PROGRESS' THEN
                        SELECT t."TenantId" INTO v_target_tenant FROM "TaskProgressReport" p JOIN "Task" t ON t."TaskId" = p."TaskId" WHERE p."ProgressReportId" = NEW."ObjectId" AND t."DeletedAt" IS NULL;
                    ELSE
                        RAISE EXCEPTION 'ATTACHMENT.INVALID_OBJECT_TYPE: Unsupported object type.' USING ERRCODE = '23514';
                    END IF;

                    IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
                        RAISE EXCEPTION 'ATTACHMENT.TARGET_NOT_FOUND: Target object not found in the exact same tenant.' USING ERRCODE = '23503';
                    END IF;

                    -- Immutability check on UPDATE
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW."TenantId" <> OLD."TenantId" OR
                           NEW."ObjectType" <> OLD."ObjectType" OR
                           NEW."ObjectId" <> OLD."ObjectId" OR
                           NEW."UploadedBy" <> OLD."UploadedBy" OR
                           NEW."ObjectKey" <> OLD."ObjectKey" THEN
                            RAISE EXCEPTION 'ATTACHMENT.IMMUTABLE_FIELDS: TenantId, ObjectType, ObjectId, UploadedBy, and ObjectKey cannot be changed.' USING ERRCODE = '23514';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_bizflow_attachment_guard ON "Attachment";
                CREATE TRIGGER trg_bizflow_attachment_guard
                    BEFORE INSERT OR UPDATE ON "Attachment"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_attachment_guard();

                -- Permissions
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a14000-0000-7000-8000-000000000003','attachments.upload','collaboration','upload','TENANT'),
                    ('01a14000-0000-7000-8000-000000000004','attachments.read','collaboration','read','TENANT'),
                    ('01a14000-0000-7000-8000-000000000005','attachments.delete','collaboration','delete','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000004'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000005'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000004'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000005'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000004'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000005');

                -- Idempotency Replay Index for Attachment Session Creation
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_AuditLog_AttachmentUploadSessionReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'ATTACHMENT.UPLOAD_SESSION_CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_bizflow_attachment_guard ON "Attachment";
                DROP FUNCTION IF EXISTS bizflow_attachment_guard();
                DROP INDEX IF EXISTS "UX_AuditLog_AttachmentUploadSessionReplay";
                DELETE FROM "RolePermission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000003', '01a14000-0000-7000-8000-000000000004', '01a14000-0000-7000-8000-000000000005');
                DELETE FROM "Permission" WHERE "PermissionId" IN ('01a14000-0000-7000-8000-000000000003', '01a14000-0000-7000-8000-000000000004', '01a14000-0000-7000-8000-000000000005');
                """);

            migrationBuilder.DropTable(
                name: "Attachment");
        }
    }
}
