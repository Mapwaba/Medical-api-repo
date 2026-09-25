using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Availability.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<string>(type: "text", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    slot_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_slots_doctor_id_slot_start",
                table: "blocked_slots",
                columns: new[] { "doctor_id", "slot_start" });

            migrationBuilder.CreateIndex(
                name: "IX_schedules_doctor_id_day",
                table: "schedules",
                columns: new[] { "doctor_id", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_slots");

            migrationBuilder.DropTable(
                name: "schedules");
        }
    }
}
