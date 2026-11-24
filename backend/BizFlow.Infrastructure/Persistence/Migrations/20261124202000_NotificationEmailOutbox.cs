using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BizFlow.Infrastructure.Persistence.Migrations;

// Infrastructure-only delivery state, maintained with narrowly scoped SQL rather than the
// business aggregate context. The trigger makes enqueue atomic for every notification producer.
[DbContext(typeof(BizFlowDbContext))]
[Migration("20261124202000_NotificationEmailOutbox")]
public sealed class NotificationEmailOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE "NotificationEmailDelivery" (
          "NotificationId" uuid PRIMARY KEY REFERENCES "Notification"("NotificationId") ON DELETE RESTRICT,
          "TenantId" uuid NOT NULL REFERENCES "Tenant"("TenantId") ON DELETE RESTRICT,
          "CreatedAt" timestamptz NOT NULL DEFAULT now(),
          "Status" varchar(20) NOT NULL DEFAULT 'PENDING' CHECK ("Status" IN ('PENDING','SENT','SKIPPED','FAILED')),
          "Attempts" integer NOT NULL DEFAULT 0 CHECK ("Attempts" BETWEEN 0 AND 5),
          "NextAttemptAt" timestamptz NOT NULL DEFAULT now(),
          "LastAttemptAt" timestamptz NULL,
          "SentAt" timestamptz NULL
        );
        CREATE INDEX "IX_NotificationEmailDelivery_Pending" ON "NotificationEmailDelivery" ("TenantId","NextAttemptAt","NotificationId") WHERE "Status"='PENDING';
        CREATE FUNCTION bizflow_enqueue_notification_email() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          -- Task deadline reminders already have their own multi-channel delivery/log pipeline.
          IF NEW."IdempotencyKey" NOT LIKE 'reminder:%' THEN
            INSERT INTO "NotificationEmailDelivery" ("NotificationId","TenantId") VALUES (NEW."NotificationId",NEW."TenantId");
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER notification_email_enqueue AFTER INSERT ON "Notification" FOR EACH ROW EXECUTE FUNCTION bizflow_enqueue_notification_email();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER notification_email_enqueue ON "Notification";
        DROP FUNCTION bizflow_enqueue_notification_email();
        DROP TABLE "NotificationEmailDelivery";
        """);
}
