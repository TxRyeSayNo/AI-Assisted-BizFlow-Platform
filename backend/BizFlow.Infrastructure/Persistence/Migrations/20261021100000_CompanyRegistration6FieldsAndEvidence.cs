using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizFlow.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BizFlowDbContext))]
    [Migration("20261021100000_CompanyRegistration6FieldsAndEvidence")]
    public partial class CompanyRegistration6FieldsAndEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Company",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonName",
                table: "Company",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Company",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentKey",
                table: "Company",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentFileName",
                table: "Company",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TaxId", table: "Company");
            migrationBuilder.DropColumn(name: "ContactPersonName", table: "Company");
            migrationBuilder.DropColumn(name: "ContactPhone", table: "Company");
            migrationBuilder.DropColumn(name: "EvidenceDocumentKey", table: "Company");
            migrationBuilder.DropColumn(name: "EvidenceDocumentFileName", table: "Company");
        }
    }
}
