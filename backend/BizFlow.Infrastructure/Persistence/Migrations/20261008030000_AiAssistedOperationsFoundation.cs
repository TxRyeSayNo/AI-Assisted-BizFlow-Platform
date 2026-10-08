using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAssistedOperationsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIInteraction",
                columns: table => new
                {
                    AIInteractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InputRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OutputRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    TokenUsage = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIInteraction", x => x.AIInteractionId);
                    table.CheckConstraint("CK_AIInteraction_Status", "\"Status\" IN ('SUCCEEDED','FAILED','TIMEOUT','REJECTED')");
                    table.ForeignKey(
                        name: "FK_AIInteraction_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIInteraction_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIRecommendation",
                columns: table => new
                {
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AIInteractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    HumanDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DecisionBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIRecommendation", x => x.RecommendationId);
                    table.CheckConstraint("CK_AIRecommendation_ObjectType", "\"ObjectType\" IN ('TASK','REQUEST')");
                    table.CheckConstraint("CK_AIRecommendation_HumanDecision", "\"HumanDecision\" IS NULL OR \"HumanDecision\" IN ('ACCEPTED','OVERRIDDEN','DISMISSED')");
                    table.ForeignKey(
                        name: "FK_AIRecommendation_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIRecommendation_AIInteraction_AIInteractionId",
                        column: x => x.AIInteractionId,
                        principalTable: "AIInteraction",
                        principalColumn: "AIInteractionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIRecommendation_User_DecisionBy",
                        column: x => x.DecisionBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIAgentAction",
                columns: table => new
                {
                    AIAgentActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AIInteractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ArgsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AuthorizationResult = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExecutionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAgentAction", x => x.AIAgentActionId);
                    table.CheckConstraint("CK_AIAgentAction_AuthorizationResult", "\"AuthorizationResult\" IN ('AUTHORIZED','DENIED')");
                    table.CheckConstraint("CK_AIAgentAction_ExecutionStatus", "\"ExecutionStatus\" IN ('NOT_EXECUTED','SUCCEEDED','FAILED')");
                    table.ForeignKey(
                        name: "FK_AIAgentAction_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIAgentAction_AIInteraction_AIInteractionId",
                        column: x => x.AIInteractionId,
                        principalTable: "AIInteraction",
                        principalColumn: "AIInteractionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIInteraction_TenantId_CreatedAt",
                table: "AIInteraction",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIInteraction_TenantId_UserId",
                table: "AIInteraction",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AIRecommendation_TenantId_ObjectType_ObjectId",
                table: "AIRecommendation",
                columns: new[] { "TenantId", "ObjectType", "ObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AIRecommendation_TenantId_AIInteractionId",
                table: "AIRecommendation",
                columns: new[] { "TenantId", "AIInteractionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AIAgentAction_TenantId_CreatedAt",
                table: "AIAgentAction",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIAgentAction_TenantId_AIInteractionId",
                table: "AIAgentAction",
                columns: new[] { "TenantId", "AIInteractionId" });

            migrationBuilder.Sql("""
                -- AI Permissions
                INSERT INTO "Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") VALUES
                    ('01a14000-0000-7000-8000-000000000010','ai.001','ai','assist_task','TENANT'),
                    ('01a14000-0000-7000-8000-000000000011','ai.002','ai','recommend_assignment','TENANT'),
                    ('01a14000-0000-7000-8000-000000000012','ai.003','ai','recommend_parameters','TENANT'),
                    ('01a14000-0000-7000-8000-000000000013','ai.004','ai','task_breakdown','TENANT'),
                    ('01a14000-0000-7000-8000-000000000014','ai.005','ai','task_risk','TENANT'),
                    ('01a14000-0000-7000-8000-000000000015','ai.006','ai','task_summary','TENANT'),
                    ('01a14000-0000-7000-8000-000000000016','ai.007','ai','request_multi_intent','TENANT'),
                    ('01a14000-0000-7000-8000-000000000017','ai.008','ai','request_split','TENANT'),
                    ('01a14000-0000-7000-8000-000000000018','ai.009','ai','request_routing','TENANT'),
                    ('01a14000-0000-7000-8000-000000000019','ai.010','ai','action_execute','TENANT');

                INSERT INTO "RolePermission" ("RoleId", "PermissionId") VALUES
                    -- COMPANY_ADMIN
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000010'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000011'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000012'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000013'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000014'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000015'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000016'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000017'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000018'),
                    ('019f7f8a-0000-7000-8000-000000000002','01a14000-0000-7000-8000-000000000019'),
                    -- MANAGER
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000010'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000011'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000012'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000013'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000014'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000015'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000016'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000017'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000018'),
                    ('019f7f8a-0000-7000-8000-000000000003','01a14000-0000-7000-8000-000000000019'),
                    -- EMPLOYEE
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000015'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000016'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000017'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000018'),
                    ('019f7f8a-0000-7000-8000-000000000004','01a14000-0000-7000-8000-000000000019');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "PermissionId" IN (
                    '01a14000-0000-7000-8000-000000000010',
                    '01a14000-0000-7000-8000-000000000011',
                    '01a14000-0000-7000-8000-000000000012',
                    '01a14000-0000-7000-8000-000000000013',
                    '01a14000-0000-7000-8000-000000000014',
                    '01a14000-0000-7000-8000-000000000015',
                    '01a14000-0000-7000-8000-000000000016',
                    '01a14000-0000-7000-8000-000000000017',
                    '01a14000-0000-7000-8000-000000000018',
                    '01a14000-0000-7000-8000-000000000019'
                );
                DELETE FROM "Permission" WHERE "PermissionId" IN (
                    '01a14000-0000-7000-8000-000000000010',
                    '01a14000-0000-7000-8000-000000000011',
                    '01a14000-0000-7000-8000-000000000012',
                    '01a14000-0000-7000-8000-000000000013',
                    '01a14000-0000-7000-8000-000000000014',
                    '01a14000-0000-7000-8000-000000000015',
                    '01a14000-0000-7000-8000-000000000016',
                    '01a14000-0000-7000-8000-000000000017',
                    '01a14000-0000-7000-8000-000000000018',
                    '01a14000-0000-7000-8000-000000000019'
                );
                """);

            migrationBuilder.DropTable(name: "AIAgentAction");
            migrationBuilder.DropTable(name: "AIRecommendation");
            migrationBuilder.DropTable(name: "AIInteraction");
        }
    }
}
