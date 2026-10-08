using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestResolutionAndConfirmationCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a12000-0000-7000-8000-00000000000b','requests.resolve','requests','resolve','TENANT'),
                    ('01a12000-0000-7000-8000-00000000000c','requests.confirm','requests','confirm','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-00000000000b'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-00000000000c'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-00000000000b'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-00000000000c'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-00000000000b'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-00000000000c');

                ALTER TABLE "AuditLog" DROP CONSTRAINT IF EXISTS "CK_AuditLog_RequestReplay";
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (
                  "Action" NOT IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED', 'REQUEST.ROUTED', 'REQUEST.RECEIVED', 'REQUEST.STARTED', 'REQUEST.REJECTED', 'REQUEST.CANCELLED', 'REQUEST.RESOLVED', 'REQUEST.CONFIRMED', 'REQUEST.REWORK', 'REQUEST.REVISED') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('REQUEST.CREATE', 'REQUEST.SUBMIT', 'REQUEST.ROUTE', 'REQUEST.RECEIVE', 'REQUEST.START', 'REQUEST.REJECT', 'REQUEST.CANCEL', 'REQUEST.RESOLVE', 'REQUEST.CONFIRM', 'REQUEST.REWORK', 'REQUEST.REVISE'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));

                CREATE UNIQUE INDEX "UX_AuditLog_RequestResolutionReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.RESOLVED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestConfirmationReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.CONFIRMED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestReworkReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.REWORK' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestRevisionReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.REVISED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action" IN ('REQUEST.RESOLVED', 'REQUEST.CONFIRMED', 'REQUEST.REWORK', 'REQUEST.REVISED') AND "MetadataJson" ? 'idempotency') THEN
                        RAISE EXCEPTION 'Request replay evidence exists; replay protection cannot be removed.' USING ERRCODE='23514';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p USING ("PermissionId")
                        WHERE p."Code" IN ('requests.resolve','requests.confirm')
                        AND NOT (
                            (p."Code" IN ('requests.resolve','requests.confirm') AND rp."RoleId" IN ('019f7f8a-0000-7000-8000-000000000002','019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004'))
                        )) THEN RAISE EXCEPTION 'Cannot discard configured Request resolution grants' USING ERRCODE='23514';
                    END IF;
                END $$;

                DROP INDEX IF EXISTS "UX_AuditLog_RequestRevisionReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestReworkReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestConfirmationReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestResolutionReplay";

                ALTER TABLE "AuditLog" DROP CONSTRAINT IF EXISTS "CK_AuditLog_RequestReplay";
                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (
                  "Action" NOT IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED', 'REQUEST.ROUTED', 'REQUEST.RECEIVED', 'REQUEST.STARTED', 'REQUEST.REJECTED', 'REQUEST.CANCELLED') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('REQUEST.CREATE', 'REQUEST.SUBMIT', 'REQUEST.ROUTE', 'REQUEST.RECEIVE', 'REQUEST.START', 'REQUEST.REJECT', 'REQUEST.CANCEL'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));

                DELETE FROM "RolePermission" WHERE "PermissionId" IN (
                    '01a12000-0000-7000-8000-00000000000b',
                    '01a12000-0000-7000-8000-00000000000c'
                );
                DELETE FROM "Permission" WHERE "PermissionId" IN (
                    '01a12000-0000-7000-8000-00000000000b',
                    '01a12000-0000-7000-8000-00000000000c'
                );
                """);
        }
    }
}
