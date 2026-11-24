using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BizFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BizFlowDbContext))]
[Migration("20261124222000_CompanyRegistrationEmail")]
public sealed class CompanyRegistrationEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE "CompanyRegistrationEmail" (
          "Id" uuid PRIMARY KEY,
          "CompanyId" uuid NOT NULL REFERENCES "Company"("CompanyId") ON DELETE RESTRICT,
          "Payload" jsonb NOT NULL,
          "Status" varchar(20) NOT NULL DEFAULT 'PENDING' CHECK ("Status" IN ('PENDING','SENT','SKIPPED','FAILED')),
          "Attempts" integer NOT NULL DEFAULT 0 CHECK ("Attempts" BETWEEN 0 AND 5),
          "CreatedAt" timestamptz NOT NULL DEFAULT now(),
          "NextAttemptAt" timestamptz NOT NULL DEFAULT now(),
          "SentAt" timestamptz NULL,
          "ConsumedAt" timestamptz NULL
        );
        CREATE INDEX "IX_CompanyRegistrationEmail_Pending" ON "CompanyRegistrationEmail" ("NextAttemptAt","Id") WHERE "Status"='PENDING';
        CREATE INDEX "IX_CompanyRegistrationEmail_Company" ON "CompanyRegistrationEmail" ("CompanyId","Id");
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("DROP TABLE \"CompanyRegistrationEmail\";");
}
