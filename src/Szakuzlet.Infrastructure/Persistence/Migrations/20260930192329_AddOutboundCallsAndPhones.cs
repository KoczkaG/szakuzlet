using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szakuzlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundCallsAndPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DialedPhoneKind",
                table: "Calls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceWorksheetId",
                table: "Calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Phones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LegallyApproved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phones_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phones_PatientId",
                table: "Phones",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phones");

            migrationBuilder.DropColumn(
                name: "DialedPhoneKind",
                table: "Calls");

            migrationBuilder.DropColumn(
                name: "ServiceWorksheetId",
                table: "Calls");
        }
    }
}
