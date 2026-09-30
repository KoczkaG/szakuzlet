using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szakuzlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCallNotesAndReferralSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DoctorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CallNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Topics = table.Column<int>(type: "integer", nullable: false),
                    ReferralSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FollowUpRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallNotes_Calls_CallId",
                        column: x => x.CallId,
                        principalTable: "Calls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CallNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CallNotes_ReferralSources_ReferralSourceId",
                        column: x => x.ReferralSourceId,
                        principalTable: "ReferralSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallNotes_CallId",
                table: "CallNotes",
                column: "CallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallNotes_PatientId",
                table: "CallNotes",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_CallNotes_ReferralSourceId",
                table: "CallNotes",
                column: "ReferralSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallNotes");

            migrationBuilder.DropTable(
                name: "ReferralSources");
        }
    }
}
