using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Appointment.Migrations
{
    /// <inheritdoc />
    public partial class RecordWhoBookedForFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_first_name",
                table: "booking_authorizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_first_name",
                table: "booking_authorizations");
        }
    }
}
