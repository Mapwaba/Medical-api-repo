using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "consultation_fee",
                table: "doctor_profiles",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consultation_fee",
                table: "doctor_profiles");
        }
    }
}
