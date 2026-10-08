using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestLifecycleAndRoutingCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestRouting",
                columns: table => new
                {
                    RequestRoutingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoutedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestRouting", x => x.RequestRoutingId);
                    table.CheckConstraint("CK_RequestRouting_Source", "\"Source\" IN ('MANUAL', 'AI', 'RULE')");
                    table.ForeignKey(
                        name: "FK_RequestRouting_Department_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_Department_ToDepartmentId",
                        column: x => x.ToDepartmentId,
                        principalTable: "Department",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Request",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_User_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_User_RoutedBy",
                        column: x => x.RoutedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestRouting_User_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestRouting_TenantId_RequestId_RoutedAt",
                table: "RequestRouting",
                columns: new[] { "TenantId", "RequestId", "RoutedAt" });

            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a12000-0000-7000-8000-000000000006','requests.route','requests','route','TENANT'),
                    ('01a12000-0000-7000-8000-000000000007','requests.receive','requests','receive','TENANT'),
                    ('01a12000-0000-7000-8000-000000000008','requests.process','requests','process','TENANT'),
                    ('01a12000-0000-7000-8000-000000000009','requests.reject','requests','reject','TENANT'),
                    ('01a12000-0000-7000-8000-00000000000a','requests.cancel','requests','cancel','SELF');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000006'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000007'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000008'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000009'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-00000000000a'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000006'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000007'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000008'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000009'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-00000000000a'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-000000000007'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-000000000008'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-00000000000a');

                ALTER TABLE "AuditLog" DROP CONSTRAINT IF EXISTS "CK_AuditLog_RequestReplay";
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (
                  "Action" NOT IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED', 'REQUEST.ROUTED', 'REQUEST.RECEIVED', 'REQUEST.STARTED', 'REQUEST.REJECTED', 'REQUEST.CANCELLED') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('REQUEST.CREATE', 'REQUEST.SUBMIT', 'REQUEST.ROUTE', 'REQUEST.RECEIVE', 'REQUEST.START', 'REQUEST.REJECT', 'REQUEST.CANCEL'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));

                CREATE UNIQUE INDEX "UX_AuditLog_RequestRoutingReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.ROUTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestReceiptReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.RECEIVED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestExecutionReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.STARTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RequestRouting") THEN
                        RAISE EXCEPTION 'Request routing records exist; table cannot be dropped.' USING ERRCODE='23514';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action" IN ('REQUEST.ROUTED', 'REQUEST.RECEIVED', 'REQUEST.STARTED', 'REQUEST.REJECTED', 'REQUEST.CANCELLED') AND "MetadataJson" ? 'idempotency') THEN
                        RAISE EXCEPTION 'Request replay evidence exists; replay protection cannot be removed.' USING ERRCODE='23514';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p USING ("PermissionId")
                        WHERE p."Code" IN ('requests.route','requests.receive','requests.process','requests.reject','requests.cancel')
                        AND NOT (
                            (p."Code" IN ('requests.route','requests.receive','requests.process','requests.reject','requests.cancel') AND rp."RoleId" IN ('019f7f8a-0000-7000-8000-000000000002','019f7f8a-0000-7000-8000-000000000003')) OR
                            (p."Code" IN ('requests.receive','requests.process','requests.cancel') AND rp."RoleId"='019f7f8a-0000-7000-8000-000000000004')
                        )) THEN RAISE EXCEPTION 'Cannot discard configured Request lifecycle grants' USING ERRCODE='23514';
                    END IF;
                END $$;

                DROP INDEX IF EXISTS "UX_AuditLog_RequestExecutionReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestReceiptReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestRoutingReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT IF EXISTS "CK_AuditLog_RequestReplay";
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (
                  "Action" NOT IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('REQUEST.CREATE', 'REQUEST.SUBMIT'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));

                DELETE FROM "RolePermission" WHERE "PermissionId" IN (
                    SELECT "PermissionId" FROM "Permission"
                    WHERE "Code" IN ('requests.route','requests.receive','requests.process','requests.reject','requests.cancel'));
                DELETE FROM "Permission" WHERE "Code" IN ('requests.route','requests.receive','requests.process','requests.reject','requests.cancel');
                """);

            migrationBuilder.DropTable(name: "RequestRouting");
        }
    }
}
