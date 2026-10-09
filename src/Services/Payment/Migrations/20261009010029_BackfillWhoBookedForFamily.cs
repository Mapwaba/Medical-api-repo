using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Payment.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWhoBookedForFamily : Migration
    {
        // Bookings made for a relative before RecordWhoBookedForFamily didn't record who booked
        // them, so that person couldn't pay. The Appointment service has always recorded it; on
        // Render (and locally) all services share one database, so copy it from there. Skipped
        // where the appointments table isn't in this database.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.appointments') IS NOT NULL THEN
                        UPDATE payments p
                        SET booked_by_user_id = a.booked_by_user_id
                        FROM appointments a
                        WHERE p.appointment_id = a.id
                          AND p.booked_by_user_id IS NULL
                          AND a.booked_by_user_id <> a.patient_id
                          AND a.booked_by_user_id <> '00000000-0000-0000-0000-000000000000';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the copied values are what the booking would record today
        }
    }
}
