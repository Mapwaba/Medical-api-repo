using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Appointment.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceReviewHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "awaiting_insurance_review",
                table: "appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_due_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "awaiting_insurance_review",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "payment_due_at",
                table: "appointments");
        }
    }
}
