using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Appointment.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceReviewDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "insurance_review_due_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "insurance_review_remind_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            // Claims already waiting when this ships get the default 24 hours from now
            migrationBuilder.Sql(
                "UPDATE appointments SET insurance_review_due_at = now() + interval '24 hours', " +
                "insurance_review_remind_at = now() + interval '12 hours' " +
                "WHERE awaiting_insurance_review AND status = 'Pending';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "insurance_review_due_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "insurance_review_remind_at",
                table: "appointments");
        }
    }
}
