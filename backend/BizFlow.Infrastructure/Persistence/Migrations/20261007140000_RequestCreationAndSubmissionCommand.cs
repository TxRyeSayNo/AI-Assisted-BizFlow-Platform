using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestCreationAndSubmissionCommand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a12000-0000-7000-8000-000000000001','requests.create','requests','create','TENANT'),
                    ('01a12000-0000-7000-8000-000000000002','requests.submit','requests','submit','SELF'),
                    ('01a12000-0000-7000-8000-000000000003','requests.read.tenant','requests','read','TENANT'),
                    ('01a12000-0000-7000-8000-000000000004','requests.read.managed','requests','read','DEPARTMENT'),
                    ('01a12000-0000-7000-8000-000000000005','requests.read.own','requests','read','SELF');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000003'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a12000-0000-7000-8000-000000000005'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000004'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a12000-0000-7000-8000-000000000005'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-000000000001'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-000000000002'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a12000-0000-7000-8000-000000000005');

                ALTER TABLE "AuditLog" ADD CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (
                  "Action" NOT IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED') OR NOT COALESCE("MetadataJson" ? 'idempotency', false) OR
                  ("TenantId" IS NOT NULL AND "ActorId" IS NOT NULL AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'operation' IN ('REQUEST.CREATE', 'REQUEST.SUBMIT'), false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'keyHash' ~ '^[0-9a-f]{64}$', false) AND
                   COALESCE("MetadataJson" -> 'idempotency' ->> 'fingerprint' ~ '^[0-9a-f]{64}$', false)));

                CREATE UNIQUE INDEX "UX_AuditLog_RequestCreationReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.CREATED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;

                CREATE UNIQUE INDEX "UX_AuditLog_RequestSubmissionReplay" ON "AuditLog"
                  ("TenantId", "ActorId", ("MetadataJson" -> 'idempotency' ->> 'keyHash'))
                  WHERE "Action" = 'REQUEST.SUBMITTED' AND "MetadataJson" -> 'idempotency' ->> 'keyHash' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AuditLog" WHERE "Action" IN ('REQUEST.CREATED', 'REQUEST.SUBMITTED') AND "MetadataJson" ? 'idempotency') THEN
                        RAISE EXCEPTION 'Request replay evidence exists; replay protection cannot be removed.' USING ERRCODE='23514';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p USING ("PermissionId")
                        WHERE p."Code" IN ('requests.create','requests.submit','requests.read.tenant','requests.read.managed','requests.read.own')
                        AND NOT (
                            (p."Code"='requests.read.tenant' AND rp."RoleId"='019f7f8a-0000-7000-8000-000000000002') OR
                            (p."Code"='requests.read.managed' AND rp."RoleId"='019f7f8a-0000-7000-8000-000000000003') OR
                            (p."Code" IN ('requests.create','requests.submit') AND rp."RoleId" IN
                                ('019f7f8a-0000-7000-8000-000000000002','019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004')) OR
                            (p."Code"='requests.read.own' AND rp."RoleId" IN
                                ('019f7f8a-0000-7000-8000-000000000002','019f7f8a-0000-7000-8000-000000000003','019f7f8a-0000-7000-8000-000000000004'))
                        )) THEN RAISE EXCEPTION 'Cannot discard configured Request grants' USING ERRCODE='23514';
                    END IF;
                END $$;

                DROP INDEX IF EXISTS "UX_AuditLog_RequestSubmissionReplay";
                DROP INDEX IF EXISTS "UX_AuditLog_RequestCreationReplay";
                ALTER TABLE "AuditLog" DROP CONSTRAINT IF EXISTS "CK_AuditLog_RequestReplay";

                DELETE FROM "RolePermission" WHERE "PermissionId" IN (SELECT "PermissionId" FROM "Permission"
                    WHERE "Code" IN ('requests.create','requests.submit','requests.read.tenant','requests.read.managed','requests.read.own'));
                DELETE FROM "Permission" WHERE "Code" IN ('requests.create','requests.submit','requests.read.tenant','requests.read.managed','requests.read.own');
                """);
        }
    }
}
