using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorClinicManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_doctor_profiles_clinics_clinic_id",
                table: "doctor_profiles");

            migrationBuilder.DropIndex(
                name: "IX_doctor_profiles_clinic_id",
                table: "doctor_profiles");

            migrationBuilder.DropColumn(
                name: "clinic_id",
                table: "doctor_profiles");

            migrationBuilder.CreateTable(
                name: "doctor_profile_clinics",
                columns: table => new
                {
                    ClinicsId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorProfilesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_profile_clinics", x => new { x.ClinicsId, x.DoctorProfilesId });
                    table.ForeignKey(
                        name: "FK_doctor_profile_clinics_clinics_ClinicsId",
                        column: x => x.ClinicsId,
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_doctor_profile_clinics_doctor_profiles_DoctorProfilesId",
                        column: x => x.DoctorProfilesId,
                        principalTable: "doctor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_profile_clinics_DoctorProfilesId",
                table: "doctor_profile_clinics",
                column: "DoctorProfilesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_profile_clinics");

            migrationBuilder.AddColumn<Guid>(
                name: "clinic_id",
                table: "doctor_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_doctor_profiles_clinic_id",
                table: "doctor_profiles",
                column: "clinic_id");

            migrationBuilder.AddForeignKey(
                name: "FK_doctor_profiles_clinics_clinic_id",
                table: "doctor_profiles",
                column: "clinic_id",
                principalTable: "clinics",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
