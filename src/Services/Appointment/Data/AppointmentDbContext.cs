using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Data;

public class AppointmentDbContext(DbContextOptions<AppointmentDbContext> options)
    : DbContext(options)
{
    public DbSet<Models.Appointment> Appointments => Set<Models.Appointment>();
    public DbSet<Models.BookingAuthorization> BookingAuthorizations => Set<Models.BookingAuthorization>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Appointment ──────────────────────────────────────────────────
        mb.Entity<Models.Appointment>(e =>
        {
            e.ToTable("appointments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RefNumber).HasColumnName("ref_number")
                .ValueGeneratedOnAdd();
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.BookedByUserId).HasColumnName("booked_by_user_id");
            e.Property(x => x.ClinicId).HasColumnName("clinic_id");
            e.Property(x => x.SlotStart).HasColumnName("slot_start");
            e.Property(x => x.SlotEnd).HasColumnName("slot_end");
            e.Property(x => x.Motif).HasColumnName("motif");
            e.Property(x => x.Status).HasColumnName("status")
                .HasConversion<string>();
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // fast lookup for the conflict check on create/reschedule
            e.HasIndex(x => new { x.DoctorId, x.SlotStart });
        });

        // ── BookingAuthorization ─────────────────────────────────────────
        mb.Entity<Models.BookingAuthorization>(e =>
        {
            e.ToTable("booking_authorizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BookerId).HasColumnName("booker_id");
            e.Property(x => x.TargetId).HasColumnName("target_id");
            e.Property(x => x.IsDependentTarget).HasColumnName("is_dependent_target");
            e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            // MassTransit is at-least-once — a redelivered event must not duplicate rows
            e.HasIndex(x => new { x.BookerId, x.TargetId }).IsUnique();
            e.HasIndex(x => x.SourceId);
        });
    }
}
