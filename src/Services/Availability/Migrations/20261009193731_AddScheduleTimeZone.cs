using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Availability.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                table: "schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Africa/Kinshasa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "time_zone",
                table: "schedules");
        }
    }
}
