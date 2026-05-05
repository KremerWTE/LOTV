using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificationsGrantsTouchpointsNotifPrefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonorTouchpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DonorId = table.Column<int>(type: "INTEGER", nullable: false),
                    TouchType = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    TouchDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StaffName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorTouchpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonorTouchpoints_Donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantorName = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    AwardedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportDueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPrefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PushEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPrefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerCertifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CertType = table.Column<string>(type: "TEXT", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerCertifications_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonorTouchpoints_DonorId",
                table: "DonorTouchpoints",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_DonorTouchpoints_TouchDate",
                table: "DonorTouchpoints",
                column: "TouchDate");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_ChapterId",
                table: "Grants",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_ReportDueDate",
                table: "Grants",
                column: "ReportDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_Status",
                table: "Grants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPrefs_UserId_EventType",
                table: "NotificationPrefs",
                columns: new[] { "UserId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerCertifications_ExpiresDate",
                table: "VolunteerCertifications",
                column: "ExpiresDate");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerCertifications_VolunteerId",
                table: "VolunteerCertifications",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerCertifications_VolunteerId_CertType",
                table: "VolunteerCertifications",
                columns: new[] { "VolunteerId", "CertType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonorTouchpoints");

            migrationBuilder.DropTable(
                name: "Grants");

            migrationBuilder.DropTable(
                name: "NotificationPrefs");

            migrationBuilder.DropTable(
                name: "VolunteerCertifications");
        }
    }
}
