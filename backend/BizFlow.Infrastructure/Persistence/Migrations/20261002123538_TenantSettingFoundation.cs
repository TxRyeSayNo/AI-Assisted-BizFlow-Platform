using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantSettingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_valid_tenant_setting(policy_key text, policy_value jsonb)
                RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
                    SELECT CASE
                        WHEN policy_key IN ('ai.enabled','ai.auto_action_enabled','sla.pause_on_waiting_for_information',
                            'request.generic_service_allowed','notification.email_enabled') THEN jsonb_typeof(policy_value)='boolean'
                        WHEN policy_key IN ('ai.auto_action_tools','attachment.allowed_content_types') THEN
                            CASE WHEN jsonb_typeof(policy_value)='array' THEN NOT EXISTS
                                (SELECT 1 FROM jsonb_array_elements(policy_value) AS item(value)
                                 WHERE jsonb_typeof(value)<>'string' OR btrim(value #>> '{}', chr(32)||chr(9)||chr(13)||chr(10))='') ELSE false END
                        WHEN policy_key IN ('ai.confidence_threshold','ai.monthly_call_limit','attachment.max_size_bytes','report.max_range_days') THEN
                            CASE WHEN jsonb_typeof(policy_value)='number' THEN
                                CASE policy_key
                                    WHEN 'ai.confidence_threshold' THEN (policy_value #>> '{}')::numeric BETWEEN 0 AND 1
                                    WHEN 'attachment.max_size_bytes' THEN (policy_value #>> '{}')::numeric BETWEEN 0 AND 524288000
                                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                                    WHEN 'report.max_range_days' THEN (policy_value #>> '{}')::numeric BETWEEN 1 AND 2147483647
                                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                                    ELSE (policy_value #>> '{}')::numeric BETWEEN 0 AND 2147483647
                                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                                END ELSE false END
                        ELSE false
                    END
                $$;
                """);
            migrationBuilder.CreateTable(
                name: "TenantSetting",
                columns: table => new
                {
                    TenantSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSetting", x => x.TenantSettingId);
                    table.CheckConstraint("CK_TenantSetting_Value", "bizflow_valid_tenant_setting(\"Key\", \"ValueJson\")");
                    table.ForeignKey(
                        name: "FK_TenantSetting_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSetting_User_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSetting_TenantId_Key",
                table: "TenantSetting",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSetting_UpdatedBy",
                table: "TenantSetting",
                column: "UpdatedBy");
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_tenant_setting_boundary() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."UpdatedBy" AND
                        ("TenantId"=NEW."TenantId" OR ("TenantId" IS NULL AND "IsPlatformAdministrator"))) THEN
                        RAISE EXCEPTION 'Setting editor must belong to the tenant or platform plane' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='UPDATE' AND (NEW."TenantSettingId" IS DISTINCT FROM OLD."TenantSettingId" OR
                        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."Key" IS DISTINCT FROM OLD."Key") THEN
                        RAISE EXCEPTION 'Setting ownership and key are immutable' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER tenant_setting_boundary BEFORE INSERT OR UPDATE ON "TenantSetting"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_tenant_setting_boundary();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TenantSetting") THEN
                        RAISE EXCEPTION 'Cannot roll back populated tenant policies' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TRIGGER tenant_setting_boundary ON "TenantSetting";
                DROP FUNCTION bizflow_tenant_setting_boundary();
                """);
            migrationBuilder.DropTable(
                name: "TenantSetting");
            migrationBuilder.Sql("DROP FUNCTION bizflow_valid_tenant_setting(text, jsonb);");
        }
    }
}
