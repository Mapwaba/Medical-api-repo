using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Payment.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    patient_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    gross_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    platform_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    provider_ref = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                filter: "[provider_ref] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
