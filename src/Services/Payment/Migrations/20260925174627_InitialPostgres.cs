using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Payment.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor_fees",
                columns: table => new
                {
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    platform_fee_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_fees", x => x.doctor_id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    platform_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: true),
                    provider_ref = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_appointment_id",
                table: "payments",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_provider_ref",
                table: "payments",
                column: "provider_ref",
                unique: true,
                filter: "provider_ref IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_fees");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
