using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarPushDonorLinkFx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DonorMagicLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DonorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorMagicLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    RateToUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    AsOf = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonorMagicLinks_DonorId",
                table: "DonorMagicLinks",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_DonorMagicLinks_Token",
                table: "DonorMagicLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CurrencyCode_AsOf",
                table: "ExchangeRates",
                columns: new[] { "CurrencyCode", "AsOf" });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonorMagicLinks");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "AspNetUsers");
        }
    }
}
