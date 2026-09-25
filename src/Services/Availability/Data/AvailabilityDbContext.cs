using LandaDoc.Availability.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Availability.Data;

public class AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options)
    : DbContext(options)
{
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<BlockedSlot> BlockedSlots => Set<BlockedSlot>();

    // Read-only view onto the `appointments` table owned by the Appointment
    // service — excluded from this context's migrations so it never tries
    // to create/alter a table it doesn't own.
    public DbSet<AppointmentSlot> Appointments => Set<AppointmentSlot>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Schedule>(e =>
        {
            e.ToTable("schedules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.Day).HasColumnName("day").HasConversion<string>();
            e.Property(x => x.OpenTime).HasColumnName("open_time");
            e.Property(x => x.CloseTime).HasColumnName("close_time");
            e.Property(x => x.SlotMinutes).HasColumnName("slot_minutes");

            e.HasIndex(x => new { x.DoctorId, x.Day });
        });

        mb.Entity<BlockedSlot>(e =>
        {
            e.ToTable("blocked_slots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.SlotStart).HasColumnName("slot_start");
            e.Property(x => x.Reason).HasColumnName("reason");

            e.HasIndex(x => new { x.DoctorId, x.SlotStart });
        });

        mb.Entity<AppointmentSlot>(e =>
        {
            e.ToTable("appointments", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.SlotStart).HasColumnName("slot_start");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        });
    }
}
