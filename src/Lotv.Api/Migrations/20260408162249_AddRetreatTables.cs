using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRetreatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Retreats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    TicketPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    GoalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    GiveButterCampaignId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retreats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetreatExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RetreatId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PaidBy = table.Column<string>(type: "TEXT", nullable: true),
                    ReceiptUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetreatExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetreatExpenses_Retreats_RetreatId",
                        column: x => x.RetreatId,
                        principalTable: "Retreats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetreatRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RetreatId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Zip = table.Column<string>(type: "TEXT", nullable: true),
                    DietaryNeeds = table.Column<string>(type: "TEXT", nullable: true),
                    AccessibilityNeeds = table.Column<string>(type: "TEXT", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "TEXT", nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "TEXT", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    RegistrationSource = table.Column<int>(type: "INTEGER", nullable: false),
                    GiveButterTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    DudaSubmissionId = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetreatRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetreatRegistrations_Retreats_RetreatId",
                        column: x => x.RetreatId,
                        principalTable: "Retreats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetreatExpenses_ChapterId",
                table: "RetreatExpenses",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatExpenses_RetreatId",
                table: "RetreatExpenses",
                column: "RetreatId");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatExpenses_RetreatId_Category",
                table: "RetreatExpenses",
                columns: new[] { "RetreatId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_ChapterId",
                table: "RetreatRegistrations",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_DudaSubmissionId",
                table: "RetreatRegistrations",
                column: "DudaSubmissionId",
                unique: true,
                filter: "[DudaSubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_GiveButterTransactionId",
                table: "RetreatRegistrations",
                column: "GiveButterTransactionId",
                unique: true,
                filter: "[GiveButterTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_PaymentStatus",
                table: "RetreatRegistrations",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_RegistrationSource",
                table: "RetreatRegistrations",
                column: "RegistrationSource");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_RetreatId",
                table: "RetreatRegistrations",
                column: "RetreatId");

            migrationBuilder.CreateIndex(
                name: "IX_RetreatRegistrations_RetreatId_ChapterId",
                table: "RetreatRegistrations",
                columns: new[] { "RetreatId", "ChapterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Retreats_ChapterId",
                table: "Retreats",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Retreats_ChapterId_Status",
                table: "Retreats",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Retreats_Date",
                table: "Retreats",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetreatExpenses");

            migrationBuilder.DropTable(
                name: "RetreatRegistrations");

            migrationBuilder.DropTable(
                name: "Retreats");
        }
    }
}
