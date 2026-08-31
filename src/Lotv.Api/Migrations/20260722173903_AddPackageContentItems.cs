using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageContentItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageContentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Packed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PackedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PackedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageContentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageContentItems_Requests_PackageRequestId",
                        column: x => x.PackageRequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageContentItems_ResourceItems_ResourceItemId",
                        column: x => x.ResourceItemId,
                        principalTable: "ResourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageContentItems_PackageRequestId",
                table: "PackageContentItems",
                column: "PackageRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageContentItems_ResourceItemId",
                table: "PackageContentItems",
                column: "ResourceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageContentItems");
        }
    }
}
