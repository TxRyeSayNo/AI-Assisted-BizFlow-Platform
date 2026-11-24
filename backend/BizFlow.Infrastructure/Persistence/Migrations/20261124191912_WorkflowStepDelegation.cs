using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowStepDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalId",
                table: "WorkflowDecision",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing decisions were made by the original candidate. Backfill before the unique index/FK.
            migrationBuilder.Sql("""
                ALTER TABLE "WorkflowDecision" DISABLE TRIGGER runtime_guard;
                UPDATE "WorkflowDecision" SET "PrincipalId"="ActorId";
                ALTER TABLE "WorkflowDecision" ENABLE TRIGGER runtime_guard;
                """);

            migrationBuilder.CreateTable(
                name: "WorkflowDelegation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDelegation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDelegation_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDelegation_User_DelegateId",
                        column: x => x.DelegateId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDelegation_User_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDelegation_User_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDelegation_WorkflowStepExecution_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "WorkflowStepExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDecision_PrincipalId",
                table: "WorkflowDecision",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDecision_StepExecutionId_PrincipalId",
                table: "WorkflowDecision",
                columns: new[] { "StepExecutionId", "PrincipalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_CreatedBy",
                table: "WorkflowDelegation",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_DelegateId",
                table: "WorkflowDelegation",
                column: "DelegateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_PrincipalId",
                table: "WorkflowDelegation",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_RevokedBy",
                table: "WorkflowDelegation",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_StepExecutionId_DelegateId",
                table: "WorkflowDelegation",
                columns: new[] { "StepExecutionId", "DelegateId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_StepExecutionId_PrincipalId",
                table: "WorkflowDelegation",
                columns: new[] { "StepExecutionId", "PrincipalId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegation_TenantId_DelegateId_ExpiresAt",
                table: "WorkflowDelegation",
                columns: new[] { "TenantId", "DelegateId", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDecision_User_PrincipalId",
                table: "WorkflowDecision",
                column: "PrincipalId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_delegation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN
                    RAISE EXCEPTION 'Workflow delegation evidence is retained' USING ERRCODE='23514';
                  END IF;
                  IF TG_TABLE_NAME='WorkflowDelegation' THEN
                    IF TG_OP='UPDATE' THEN
                      IF OLD."RevokedAt" IS NOT NULL OR NEW."RevokedAt" IS NULL OR NEW."RevokedAt" < OLD."CreatedAt" OR NEW."RevokedBy" IS NULL OR
                        (to_jsonb(NEW)-ARRAY['RevokedAt','RevokedBy']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['RevokedAt','RevokedBy']) OR
                        NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."RevokedBy" AND "TenantId"=NEW."TenantId") THEN
                        RAISE EXCEPTION 'Delegation evidence is immutable except revocation' USING ERRCODE='23514';
                      END IF;
                      RETURN NEW;
                    END IF;
                    IF NEW."RevokedAt" IS NOT NULL OR NEW."RevokedBy" IS NOT NULL OR NEW."ExpiresAt" <= NEW."CreatedAt" OR
                      NEW."ExpiresAt" > NEW."CreatedAt" + interval '90 days' OR btrim(NEW."Reason")='' OR
                      NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s JOIN "WorkflowInstance" i ON i."Id"=s."InstanceId"
                        JOIN "Request" r ON r."RequestId"=i."RequestId" JOIN "User" u ON u."UserId"=NEW."DelegateId"
                        JOIN "User" a ON a."UserId"=NEW."CreatedBy"
                        WHERE s."Id"=NEW."StepExecutionId" AND s."InstanceId"=NEW."InstanceId" AND s."TenantId"=NEW."TenantId" AND
                        i."Status"='RUNNING' AND s."Status"='PENDING' AND s."NodeType" IN ('APPROVAL','DEPARTMENT') AND
                        s."CandidatesJson" @> jsonb_build_array(NEW."PrincipalId"::text) AND
                        NOT s."CandidatesJson" @> jsonb_build_array(NEW."DelegateId"::text) AND
                        (s."NodeType" <> 'APPROVAL' OR r."RequesterId" <> NEW."DelegateId") AND
                        u."TenantId"=NEW."TenantId" AND u."Status"='ACTIVE' AND u."DeletedAt" IS NULL AND a."TenantId"=NEW."TenantId") OR
                      EXISTS (SELECT 1 FROM "WorkflowDecision" d WHERE d."StepExecutionId"=NEW."StepExecutionId" AND
                        (d."PrincipalId"=NEW."PrincipalId" OR d."ActorId"=NEW."DelegateId")) THEN
                      RAISE EXCEPTION 'Invalid delegation slot, recipient or time range' USING ERRCODE='23514';
                    END IF;
                  ELSIF TG_TABLE_NAME='WorkflowDecision' AND TG_OP='INSERT' THEN
                    IF NOT EXISTS (SELECT 1 FROM "WorkflowStepExecution" s WHERE s."Id"=NEW."StepExecutionId" AND
                        s."CandidatesJson" @> jsonb_build_array(NEW."PrincipalId"::text)) OR
                      (NEW."ActorId"=NEW."PrincipalId" AND EXISTS (SELECT 1 FROM "WorkflowDelegation" d WHERE d."StepExecutionId"=NEW."StepExecutionId" AND
                        d."PrincipalId"=NEW."PrincipalId" AND d."CreatedAt"<=NEW."DecidedAt" AND d."ExpiresAt">NEW."DecidedAt" AND d."RevokedAt" IS NULL)) OR
                      (NEW."ActorId"<>NEW."PrincipalId" AND NOT EXISTS (SELECT 1 FROM "WorkflowDelegation" d WHERE d."StepExecutionId"=NEW."StepExecutionId" AND
                        d."PrincipalId"=NEW."PrincipalId" AND d."DelegateId"=NEW."ActorId" AND d."CreatedAt"<=NEW."DecidedAt" AND d."ExpiresAt">NEW."DecidedAt" AND d."RevokedAt" IS NULL)) THEN
                      RAISE EXCEPTION 'Decision must represent an original slot or an active delegation' USING ERRCODE='23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER delegation_guard BEFORE INSERT OR UPDATE OR DELETE ON "WorkflowDelegation" FOR EACH ROW EXECUTE FUNCTION bizflow_delegation_guard();
                CREATE TRIGGER delegation_truncate BEFORE TRUNCATE ON "WorkflowDelegation" FOR EACH STATEMENT EXECUTE FUNCTION bizflow_delegation_guard();
                CREATE TRIGGER decision_principal_guard BEFORE INSERT ON "WorkflowDecision" FOR EACH ROW EXECUTE FUNCTION bizflow_delegation_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "WorkflowDelegation") OR EXISTS (SELECT 1 FROM "WorkflowDecision" WHERE "ActorId"<>"PrincipalId") THEN
                    RAISE EXCEPTION 'Delegation history cannot be discarded' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP TRIGGER decision_principal_guard ON "WorkflowDecision";
                DROP TRIGGER delegation_guard ON "WorkflowDelegation";
                DROP TRIGGER delegation_truncate ON "WorkflowDelegation";
                DROP FUNCTION bizflow_delegation_guard();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDecision_User_PrincipalId",
                table: "WorkflowDecision");

            migrationBuilder.DropTable(
                name: "WorkflowDelegation");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDecision_PrincipalId",
                table: "WorkflowDecision");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDecision_StepExecutionId_PrincipalId",
                table: "WorkflowDecision");

            migrationBuilder.DropColumn(
                name: "PrincipalId",
                table: "WorkflowDecision");
        }
    }
}
