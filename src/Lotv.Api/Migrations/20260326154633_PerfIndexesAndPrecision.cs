using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class PerfIndexesAndPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PartnerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: true),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonorPledges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DonorId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    PledgedAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    FulfilledAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    TargetDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Campaign = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorPledges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonorPledges_Donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringDonations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DonorId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    NextChargeDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastChargedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndsOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Campaign = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringDonations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringDonations_Donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    QuantityOnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityReserved = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceItems_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToPhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MessageType = table.Column<int>(type: "INTEGER", nullable: false),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WishListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChapterId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityRequested = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityFulfilled = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FulfilledByDonorId = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishListItems_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_ChapterId_Status",
                table: "Volunteers",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ChapterId_AssignedToId",
                table: "Requests",
                columns: new[] { "ChapterId", "AssignedToId" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ChapterId_Status",
                table: "Requests",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ChapterId_Status_CreatedAt",
                table: "Requests",
                columns: new[] { "ChapterId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FundAllocations_Status",
                table: "FundAllocations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ChapterId_PaidAt",
                table: "Expenses",
                columns: new[] { "ChapterId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Date",
                table: "Events",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status_Date",
                table: "Events",
                columns: new[] { "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Donors_ChapterId_TotalGiven",
                table: "Donors",
                columns: new[] { "ChapterId", "TotalGiven" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_AllocationStatus",
                table: "Donations",
                column: "AllocationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Channel",
                table: "Donations",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ChapterId_Channel_Date",
                table: "Donations",
                columns: new[] { "ChapterId", "Channel", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ChapterId_Date",
                table: "Donations",
                columns: new[] { "ChapterId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Entity",
                table: "AuditEntries",
                column: "Entity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Entity_Timestamp",
                table: "AuditEntries",
                columns: new[] { "Entity", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_IsActive",
                table: "ApiKeys",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonorPledges_ChapterId",
                table: "DonorPledges",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_DonorPledges_ChapterId_Status",
                table: "DonorPledges",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DonorPledges_DonorId",
                table: "DonorPledges",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_DonorPledges_Status",
                table: "DonorPledges",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DonorPledges_TargetDate",
                table: "DonorPledges",
                column: "TargetDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDonations_ChapterId",
                table: "RecurringDonations",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDonations_ChapterId_Status",
                table: "RecurringDonations",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDonations_DonorId",
                table: "RecurringDonations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDonations_NextChargeDate",
                table: "RecurringDonations",
                column: "NextChargeDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringDonations_Status",
                table: "RecurringDonations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceItems_ChapterId",
                table: "ResourceItems",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_CaseId",
                table: "SmsLogs",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_MessageType",
                table: "SmsLogs",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_SentAt",
                table: "SmsLogs",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_ChapterId",
                table: "WishListItems",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_ChapterId_Status",
                table: "WishListItems",
                columns: new[] { "ChapterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_FamilyId",
                table: "WishListItems",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_Status",
                table: "WishListItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "DonorPledges");

            migrationBuilder.DropTable(
                name: "RecurringDonations");

            migrationBuilder.DropTable(
                name: "ResourceItems");

            migrationBuilder.DropTable(
                name: "SmsLogs");

            migrationBuilder.DropTable(
                name: "WishListItems");

            migrationBuilder.DropIndex(
                name: "IX_Volunteers_ChapterId_Status",
                table: "Volunteers");

            migrationBuilder.DropIndex(
                name: "IX_Requests_ChapterId_AssignedToId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_ChapterId_Status",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_ChapterId_Status_CreatedAt",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_FundAllocations_Status",
                table: "FundAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ChapterId_PaidAt",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Events_Date",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_Status_Date",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Donors_ChapterId_TotalGiven",
                table: "Donors");

            migrationBuilder.DropIndex(
                name: "IX_Donations_AllocationStatus",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_Channel",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_ChapterId_Channel_Date",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_ChapterId_Date",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Entity",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Entity_Timestamp",
                table: "AuditEntries");
        }
    }
}
