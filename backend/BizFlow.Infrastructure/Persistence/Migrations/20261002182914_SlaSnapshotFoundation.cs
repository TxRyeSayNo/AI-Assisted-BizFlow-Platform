using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaSnapshotFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessCalendar",
                columns: table => new
                {
                    CalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkingHoursJson = table.Column<string>(type: "jsonb", nullable: false),
                    HolidaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCalendar", x => x.CalendarId);
                    table.CheckConstraint("CK_BusinessCalendar_Holidays", "jsonb_typeof(\"HolidaysJson\") = 'array'");
                    table.CheckConstraint("CK_BusinessCalendar_Hours", "jsonb_typeof(\"WorkingHoursJson\") = 'object'");
                    table.ForeignKey(
                        name: "FK_BusinessCalendar_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SLAVersion",
                columns: table => new
                {
                    SLAVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SLAProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    TargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    WarningMinutes = table.Column<int>(type: "integer", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalationConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SLAVersion", x => x.SLAVersionId);
                    table.CheckConstraint("CK_SLAVersion_Escalation", "\"EscalationConfigJson\" IS NULL OR jsonb_typeof(\"EscalationConfigJson\") = 'object'");
                    table.CheckConstraint("CK_SLAVersion_Number", "\"VersionNo\" > 0");
                    table.CheckConstraint("CK_SLAVersion_Thresholds", "\"TargetMinutes\" > 0 AND \"WarningMinutes\" >= 0 AND \"WarningMinutes\" < \"TargetMinutes\"");
                    table.ForeignKey(
                        name: "FK_SLAVersion_BusinessCalendar_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "BusinessCalendar",
                        principalColumn: "CalendarId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SLAVersion_SLAProfile_SLAProfileId",
                        column: x => x.SLAProfileId,
                        principalTable: "SLAProfile",
                        principalColumn: "SLAProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCalendar_TenantId",
                table: "BusinessCalendar",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SLAVersion_CalendarId",
                table: "SLAVersion",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_SLAVersion_SLAProfileId_VersionNo",
                table: "SLAVersion",
                columns: new[] { "SLAProfileId", "VersionNo" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION bizflow_calendar_schema(hours jsonb, holidays jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE day record; slot jsonb; holiday jsonb; start_min integer; end_min integer; previous_end integer;
                BEGIN
                    IF jsonb_typeof(hours) IS DISTINCT FROM 'object' OR jsonb_typeof(holidays) IS DISTINCT FROM 'array' THEN RETURN false; END IF;
                    FOR day IN SELECT key, value FROM jsonb_each(hours) LOOP
                        IF day.key NOT IN ('monday','tuesday','wednesday','thursday','friday','saturday','sunday') OR jsonb_typeof(day.value) <> 'array' THEN RETURN false; END IF;
                        FOR slot IN SELECT value FROM jsonb_array_elements(day.value) LOOP
                            IF jsonb_typeof(slot) <> 'object' THEN RETURN false; END IF;
                            IF (SELECT count(*) FROM jsonb_object_keys(slot)) <> 2 OR
                                jsonb_typeof(slot->'start') IS DISTINCT FROM 'string' OR jsonb_typeof(slot->'end') IS DISTINCT FROM 'string' OR
                                (slot->>'start') !~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' OR
                                (slot->>'end') !~ '^(([01][0-9]|2[0-3]):[0-5][0-9]|24:00)$' THEN RETURN false; END IF;
                        END LOOP;
                        previous_end := -1;
                        FOR slot IN SELECT value FROM jsonb_array_elements(day.value) ORDER BY (value->>'start') COLLATE "C" LOOP
                            start_min := substring(slot->>'start',1,2)::integer * 60 + substring(slot->>'start',4,2)::integer;
                            end_min := substring(slot->>'end',1,2)::integer * 60 + substring(slot->>'end',4,2)::integer;
                            IF end_min <= start_min OR start_min < previous_end THEN RETURN false; END IF;
                            previous_end := end_min;
                        END LOOP;
                    END LOOP;
                    IF (SELECT count(*) FROM jsonb_array_elements(holidays)) <> (SELECT count(DISTINCT value) FROM jsonb_array_elements(holidays)) THEN RETURN false; END IF;
                    FOR holiday IN SELECT value FROM jsonb_array_elements(holidays) LOOP
                        IF jsonb_typeof(holiday) <> 'string' OR (holiday#>>'{}') !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN RETURN false; END IF;
                        PERFORM make_date(substring(holiday#>>'{}',1,4)::integer, substring(holiday#>>'{}',6,2)::integer, substring(holiday#>>'{}',9,2)::integer);
                    END LOOP;
                    RETURN true;
                EXCEPTION WHEN datetime_field_overflow OR invalid_datetime_format THEN RETURN false;
                END $$;

                CREATE FUNCTION bizflow_escalation_schema(config jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE item jsonb; expected_level integer := 1; previous_offset integer := -1; offset_minutes integer;
                BEGIN
                    IF config IS NULL THEN RETURN true; END IF;
                    IF jsonb_typeof(config) IS DISTINCT FROM 'object' THEN RETURN false; END IF;
                    IF (SELECT count(*) FROM jsonb_object_keys(config)) <> 1 OR jsonb_typeof(config->'levels') IS DISTINCT FROM 'array' THEN RETURN false; END IF;
                    FOR item IN SELECT value FROM jsonb_array_elements(config->'levels') LOOP
                        IF jsonb_typeof(item) <> 'object' THEN RETURN false; END IF;
                        IF (SELECT count(*) FROM jsonb_object_keys(item)) <> 3 OR jsonb_typeof(item->'level') IS DISTINCT FROM 'number' OR
                            jsonb_typeof(item->'afterMinutes') IS DISTINCT FROM 'number' OR jsonb_typeof(item->'recipientUserIds') IS DISTINCT FROM 'array' THEN RETURN false; END IF;
                        IF (item->>'level') !~ '^[0-9]+$' OR (item->>'afterMinutes') !~ '^[0-9]+$' OR (item->>'level')::integer <> expected_level THEN RETURN false; END IF;
                        offset_minutes := (item->>'afterMinutes')::integer;
                        IF offset_minutes <= previous_offset OR jsonb_array_length(item->'recipientUserIds') = 0 THEN RETURN false; END IF;
                        IF EXISTS (SELECT 1 FROM jsonb_array_elements(item->'recipientUserIds') WHERE jsonb_typeof(value) <> 'string' OR
                            (value#>>'{}') !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' OR
                            (value#>>'{}') = '00000000-0000-0000-0000-000000000000') THEN RETURN false; END IF;
                        IF (SELECT count(*) FROM jsonb_array_elements_text(item->'recipientUserIds')) <>
                            (SELECT count(DISTINCT lower(value)) FROM jsonb_array_elements_text(item->'recipientUserIds')) THEN RETURN false; END IF;
                        expected_level := expected_level + 1; previous_offset := offset_minutes;
                    END LOOP;
                    RETURN true;
                EXCEPTION WHEN numeric_value_out_of_range OR invalid_text_representation THEN RETURN false;
                END $$;

                CREATE FUNCTION bizflow_calendar_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF EXISTS (SELECT 1 FROM "SLAVersion" WHERE "CalendarId"=OLD."CalendarId") THEN
                            RAISE EXCEPTION 'Referenced SLA calendars are immutable' USING ERRCODE='23514';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW."CalendarId" IS DISTINCT FROM OLD."CalendarId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" THEN
                            RAISE EXCEPTION 'Calendar identity and tenant are immutable' USING ERRCODE='23514';
                        END IF;
                        -- A value-preserving row touch is used by reference creation solely for MVCC serialization.
                        IF NEW IS NOT DISTINCT FROM OLD THEN RETURN NEW; END IF;
                        IF EXISTS (SELECT 1 FROM "SLAVersion" WHERE "CalendarId"=OLD."CalendarId") THEN
                            RAISE EXCEPTION 'Referenced SLA calendars are immutable' USING ERRCODE='23514';
                        END IF;
                    END IF;
                    IF NOT bizflow_calendar_schema(NEW."WorkingHoursJson", NEW."HolidaysJson") OR
                        NOT EXISTS (SELECT 1 FROM pg_timezone_names WHERE name=NEW."TimeZone") THEN
                        RAISE EXCEPTION 'Invalid business calendar configuration' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER calendar_guard BEFORE INSERT OR UPDATE OR DELETE ON "BusinessCalendar"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_calendar_guard();

                CREATE FUNCTION bizflow_sla_version_references() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE profile_tenant uuid; calendar_tenant uuid; hours jsonb; recipient uuid;
                    recipient_tenant uuid; recipient_status text; recipient_deleted timestamptz;
                BEGIN
                    IF NOT bizflow_escalation_schema(NEW."EscalationConfigJson") THEN
                        RAISE EXCEPTION 'Invalid escalation configuration' USING ERRCODE='23514';
                    END IF;
                    SELECT "TenantId" INTO profile_tenant FROM "SLAProfile" WHERE "SLAProfileId"=NEW."SLAProfileId" FOR KEY SHARE;
                    -- A lock alone is insufficient when a calendar editor has an older repeatable-read snapshot.
                    -- Touch xmin without changing any calendar values; such an editor must then serialize or abort.
                    UPDATE "BusinessCalendar" SET "TimeZone"="TimeZone" WHERE "CalendarId"=NEW."CalendarId"
                        RETURNING "TenantId", "WorkingHoursJson" INTO calendar_tenant, hours;
                    IF profile_tenant IS NULL OR calendar_tenant IS NULL OR profile_tenant <> calendar_tenant THEN
                        RAISE EXCEPTION 'SLA profile and calendar must belong to the same tenant' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM jsonb_each(hours) WHERE jsonb_array_length(value) > 0) THEN
                        RAISE EXCEPTION 'An SLA requires a calendar with working time' USING ERRCODE='23514';
                    END IF;
                    FOR recipient IN SELECT DISTINCT target.value::uuid FROM jsonb_array_elements(NEW."EscalationConfigJson"->'levels') AS level(value)
                        CROSS JOIN LATERAL jsonb_array_elements_text(level.value->'recipientUserIds') AS target(value) ORDER BY 1 LOOP
                        SELECT "TenantId", "Status"::text, "DeletedAt" INTO recipient_tenant, recipient_status, recipient_deleted
                            FROM "User" WHERE "UserId"=recipient FOR SHARE;
                        IF NOT FOUND OR recipient_tenant IS DISTINCT FROM profile_tenant OR recipient_status <> 'ACTIVE' OR recipient_deleted IS NOT NULL THEN
                            RAISE EXCEPTION 'Escalation recipients must be active users in the same tenant' USING ERRCODE='23514';
                        END IF;
                    END LOOP;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sla_version_references BEFORE INSERT ON "SLAVersion"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_sla_version_references();
                CREATE FUNCTION bizflow_sla_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Saved SLA versions are immutable' USING ERRCODE='23514';
                END $$;
                CREATE TRIGGER sla_version_immutable BEFORE UPDATE OR DELETE ON "SLAVersion"
                    FOR EACH ROW EXECUTE FUNCTION bizflow_sla_version_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SLAVersion") OR EXISTS (SELECT 1 FROM "BusinessCalendar") THEN
                        RAISE EXCEPTION 'Cannot discard SLA version or calendar snapshots' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TRIGGER sla_version_immutable ON "SLAVersion";
                DROP TRIGGER sla_version_references ON "SLAVersion";
                DROP TRIGGER calendar_guard ON "BusinessCalendar";
                DROP FUNCTION bizflow_sla_version_immutable();
                DROP FUNCTION bizflow_sla_version_references();
                DROP FUNCTION bizflow_calendar_guard();
                DROP FUNCTION bizflow_escalation_schema(jsonb);
                DROP FUNCTION bizflow_calendar_schema(jsonb, jsonb);
                """);
            migrationBuilder.DropTable(
                name: "SLAVersion");

            migrationBuilder.DropTable(
                name: "BusinessCalendar");
        }
    }
}
