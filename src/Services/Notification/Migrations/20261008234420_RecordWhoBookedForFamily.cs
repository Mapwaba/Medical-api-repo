using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Notification.Migrations
{
    /// <inheritdoc />
    public partial class RecordWhoBookedForFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booked_by_user_id",
                table: "appointment_pending_projections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_name",
                table: "appointment_pending_projections",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointment_pending_projections_booked_by_user_id_status",
                table: "appointment_pending_projections",
                columns: new[] { "booked_by_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_appointment_pending_projections_booked_by_user_id_status",
                table: "appointment_pending_projections");

            migrationBuilder.DropColumn(
                name: "booked_by_user_id",
                table: "appointment_pending_projections");

            migrationBuilder.DropColumn(
                name: "patient_name",
                table: "appointment_pending_projections");
        }
    }
}
