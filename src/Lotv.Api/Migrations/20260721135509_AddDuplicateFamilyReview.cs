using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateFamilyReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DuplicateMatchReason",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsDuplicateReview",
                table: "Requests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PossibleDuplicateFamilyId",
                table: "Requests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_NeedsDuplicateReview",
                table: "Requests",
                column: "NeedsDuplicateReview");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PossibleDuplicateFamilyId",
                table: "Requests",
                column: "PossibleDuplicateFamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_Families_PossibleDuplicateFamilyId",
                table: "Requests",
                column: "PossibleDuplicateFamilyId",
                principalTable: "Families",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_Families_PossibleDuplicateFamilyId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_NeedsDuplicateReview",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_PossibleDuplicateFamilyId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "DuplicateMatchReason",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "NeedsDuplicateReview",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PossibleDuplicateFamilyId",
                table: "Requests");
        }
    }
}
