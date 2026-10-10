using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "clinics",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "clinics");
        }
    }
}
