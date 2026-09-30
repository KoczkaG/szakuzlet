using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szakuzlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Modul2_BillingEpEanTerminalSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedEanCode",
                table: "Invoices",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingParty_Address",
                table: "Invoices",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingParty_Name",
                table: "Invoices",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingParty_TaxNumber",
                table: "Invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EInvoiceReference",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpBeneficiaryName",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpMemberId",
                table: "Invoices",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpName",
                table: "Invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrescription",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EanCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedToInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EanCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HealthFunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrictBilling = table.Column<bool>(type: "boolean", nullable: false),
                    OfficialAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TaxNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthFunds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EanCodes_Code",
                table: "EanCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EanCodes_Used",
                table: "EanCodes",
                column: "Used");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EanCodes");

            migrationBuilder.DropTable(
                name: "HealthFunds");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Number",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AssignedEanCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingParty_Address",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingParty_Name",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingParty_TaxNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "EInvoiceReference",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "EpBeneficiaryName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "EpMemberId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "EpName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsPrescription",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Invoices");
        }
    }
}
