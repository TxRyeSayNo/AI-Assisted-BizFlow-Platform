using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BizFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BizFlowDbContext))]
[Migration("20261125010000_NotificationPreferences")]
public sealed class NotificationPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE "NotificationPreference" (
          "UserId" uuid PRIMARY KEY REFERENCES "User"("UserId") ON DELETE RESTRICT,
          "TenantId" uuid NOT NULL REFERENCES "Tenant"("TenantId") ON DELETE RESTRICT,
          "EmailEnabled" boolean NOT NULL DEFAULT true,
          "DeadlineEmailMode" varchar(12) NOT NULL DEFAULT 'AwayOnly' CHECK ("DeadlineEmailMode" IN ('Always','AwayOnly','Off')),
          "DailyDigestEnabled" boolean NOT NULL DEFAULT false,
          "DigestHour" integer NOT NULL DEFAULT 8 CHECK ("DigestHour" BETWEEN 0 AND 23),
          "DigestDays" integer NOT NULL DEFAULT 1 CHECK ("DigestDays" BETWEEN 1 AND 30),
          "DigestEmail" boolean NOT NULL DEFAULT true
        );
        CREATE FUNCTION bizflow_notification_preference_owner() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."UserId" AND "TenantId"=NEW."TenantId") OR
            (TG_OP='UPDATE' AND (NEW."UserId"<>OLD."UserId" OR NEW."TenantId"<>OLD."TenantId")) THEN
            RAISE EXCEPTION 'Invalid notification preference ownership' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER notification_preference_owner BEFORE INSERT OR UPDATE ON "NotificationPreference" FOR EACH ROW EXECUTE FUNCTION bizflow_notification_preference_owner();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DROP TABLE "NotificationPreference";
        DROP FUNCTION bizflow_notification_preference_owner();
        """);
}
