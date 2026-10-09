using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_data_url",
                table: "doctor_profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_data_url",
                table: "doctor_profiles");
        }
    }
}
