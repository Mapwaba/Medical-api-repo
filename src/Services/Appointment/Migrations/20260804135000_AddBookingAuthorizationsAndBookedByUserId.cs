using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Appointment.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAuthorizationsAndBookedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booked_by_user_id",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill existing rows: before this feature, the booker was always the patient.
            migrationBuilder.Sql("UPDATE appointments SET booked_by_user_id = patient_id;");

            migrationBuilder.CreateTable(
                name: "booking_authorizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_dependent_target = table.Column<bool>(type: "boolean", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_authorizations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_authorizations_booker_id_target_id",
                table: "booking_authorizations",
                columns: new[] { "booker_id", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_authorizations_source_id",
                table: "booking_authorizations",
                column: "source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_authorizations");

            migrationBuilder.DropColumn(
                name: "booked_by_user_id",
                table: "appointments");
        }
    }
}
