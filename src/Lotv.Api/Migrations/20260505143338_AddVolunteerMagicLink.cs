using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerMagicLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VolunteerMagicLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerMagicLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerMagicLinks_Token",
                table: "VolunteerMagicLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerMagicLinks_VolunteerId",
                table: "VolunteerMagicLinks",
                column: "VolunteerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolunteerMagicLinks");
        }
    }
}
