using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Notification.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWhoBookedForFamily : Migration
    {
        // Bookings made for a relative before RecordWhoBookedForFamily didn't record who booked
        // them, so that person wasn't told about them. Copy who booked from the Appointment
        // service, and the patient's first name from Identity (an account or a dependant); on
        // Render (and locally) all services share one database. Skipped where those tables
        // aren't in this database.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.appointments') IS NOT NULL
                       AND to_regclass('public.users') IS NOT NULL
                       AND to_regclass('public.dependents') IS NOT NULL THEN
                        UPDATE appointment_pending_projections pr
                        SET booked_by_user_id = a.booked_by_user_id,
                            patient_name = coalesce(u.first_name, d.first_name)
                        FROM appointments a
                        LEFT JOIN users u ON u.id = a.patient_id
                        LEFT JOIN dependents d ON d.id = a.patient_id
                        WHERE pr.appointment_id = a.id
                          AND pr.booked_by_user_id IS NULL
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
