using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkplaceDetailsAndDoctorServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "clinics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "clinics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_data_url",
                table: "clinics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "clinics",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "doctor_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_services", x => x.id);
                    table.ForeignKey(
                        name: "FK_doctor_services_doctor_profiles_doctor_profile_id",
                        column: x => x.doctor_profile_id,
                        principalTable: "doctor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_services_doctor_profile_id",
                table: "doctor_services",
                column: "doctor_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_services");

            migrationBuilder.DropColumn(
                name: "description",
                table: "clinics");

            migrationBuilder.DropColumn(
                name: "email",
                table: "clinics");

            migrationBuilder.DropColumn(
                name: "logo_data_url",
                table: "clinics");

            migrationBuilder.DropColumn(
                name: "website",
                table: "clinics");
        }
    }
}
