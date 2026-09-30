using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Szakuzlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Modul3_ContractsExpressPostalClosureTelemedicine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceSerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaskModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PressureCmH2O = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DepositAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    SignatureMethod = table.Column<int>(type: "integer", nullable: true),
                    SignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SignedFromIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PdfReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ScannedReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DepositInvoiceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EducationAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    VideosAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    RoutineUserWaiver = table.Column<bool>(type: "boolean", nullable: false),
                    Products = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EducationAcknowledgements_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EducationVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsGeneric = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationVideos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpressIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterRole = table.Column<int>(type: "integer", nullable: false),
                    RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequesterPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RequesterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaskModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PressureCmH2O = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    PayableAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpressIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpressIntakes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostalTrials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaskModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayableAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrialDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    MaskSwapDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    ControlDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostalTrials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostalTrials_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Number",
                table: "Contracts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PatientId",
                table: "Contracts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EducationAcknowledgements_PatientId",
                table: "EducationAcknowledgements",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationVideos_ProductModel",
                table: "EducationVideos",
                column: "ProductModel");

            migrationBuilder.CreateIndex(
                name: "IX_ExpressIntakes_PatientId",
                table: "ExpressIntakes",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpressIntakes_Status",
                table: "ExpressIntakes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PostalTrials_OrderNumber",
                table: "PostalTrials",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostalTrials_PatientId",
                table: "PostalTrials",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PostalTrials_Status",
                table: "PostalTrials",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "EducationAcknowledgements");

            migrationBuilder.DropTable(
                name: "EducationVideos");

            migrationBuilder.DropTable(
                name: "ExpressIntakes");

            migrationBuilder.DropTable(
                name: "PostalTrials");
        }
    }
}
