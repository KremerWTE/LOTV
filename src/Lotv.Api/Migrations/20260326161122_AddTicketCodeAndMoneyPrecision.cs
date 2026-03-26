using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotv.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCodeAndMoneyPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TicketCode",
                table: "EventAttendees",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_EventAttendees_TicketCode",
                table: "EventAttendees",
                column: "TicketCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventAttendees_TicketCode",
                table: "EventAttendees");

            migrationBuilder.DropColumn(
                name: "TicketCode",
                table: "EventAttendees");
        }
    }
}
