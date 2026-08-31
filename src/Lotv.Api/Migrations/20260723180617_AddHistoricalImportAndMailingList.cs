using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalImportAndMailingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfLoss",
                table: "Families",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                table: "Families",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MailingListEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FamilyId = table.Column<int>(type: "INTEGER", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    MotherName = table.Column<string>(type: "TEXT", nullable: false),
                    FatherName = table.Column<string>(type: "TEXT", nullable: true),
                    StreetAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Apt = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Zip = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    MothersDayOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    FlaggedForReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewNote = table.Column<string>(type: "TEXT", nullable: true),
                    Sent = table.Column<bool>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailingListEntries_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailingListEntries_FamilyId",
                table: "MailingListEntries",
                column: "FamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailingListEntries");

            migrationBuilder.DropColumn(
                name: "DateOfLoss",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "IsHistorical",
                table: "Families");
        }
    }
}
