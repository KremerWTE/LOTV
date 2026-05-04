using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoPrivacyGbDonationReportLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Requests",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Requests",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrivacyPreference",
                table: "Families",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GiveButterTransactionId",
                table: "Donations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportType = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecipientEmails = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RecordsIncluded = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRunLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_GiveButterTransactionId",
                table: "Donations",
                column: "GiveButterTransactionId",
                unique: true,
                filter: "[GiveButterTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRunLogs_GeneratedAt",
                table: "ReportRunLogs",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRunLogs_ReportType_ChapterId",
                table: "ReportRunLogs",
                columns: new[] { "ReportType", "ChapterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportRunLogs");

            migrationBuilder.DropIndex(
                name: "IX_Donations_GiveButterTransactionId",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PrivacyPreference",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "GiveButterTransactionId",
                table: "Donations");
        }
    }
}
