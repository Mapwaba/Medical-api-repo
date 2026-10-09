using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Appointment.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWhoBookedForFamily : Migration
    {
        // Family links and dependants added before RecordWhoBookedForFamily have no first name on
        // their booking permission, so the booker's messages would say "votre proche" instead of
        // the name. Copy it from Identity (an account or a dependant); on Render (and locally) all
        // services share one database. Skipped where those tables aren't in this database.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.users') IS NOT NULL AND to_regclass('public.dependents') IS NOT NULL THEN
                        UPDATE booking_authorizations b
                        SET target_first_name = coalesce(u.first_name, d.first_name)
                        FROM booking_authorizations t
                        LEFT JOIN users u ON u.id = t.target_id
                        LEFT JOIN dependents d ON d.id = t.target_id
                        WHERE b.id = t.id
                          AND b.target_first_name IS NULL;
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
