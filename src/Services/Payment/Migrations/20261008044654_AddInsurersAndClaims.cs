using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Payment.Migrations
{
    /// <inheritdoc />
    public partial class AddInsurersAndClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "insurance_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurer_name = table.Column<string>(type: "text", nullable: false),
                    member_number = table.Column<string>(type: "text", nullable: false),
                    member_name = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    insurer_reference = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurance_claims", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "insurers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_insurance_claims_appointment_id",
                table: "insurance_claims",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "IX_insurance_claims_doctor_id",
                table: "insurance_claims",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "IX_insurers_name",
                table: "insurers",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "insurance_claims");

            migrationBuilder.DropTable(
                name: "insurers");
        }
    }
}
