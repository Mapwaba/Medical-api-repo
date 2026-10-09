using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Data;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    public DbSet<Models.Notification> Notifications => Set<Models.Notification>();
    public DbSet<Models.UserContact> UserContacts => Set<Models.UserContact>();
    public DbSet<Models.AppointmentPendingProjection> AppointmentPendingProjections => Set<Models.AppointmentPendingProjection>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Models.Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.Payload).HasColumnName("payload");
            e.Property(x => x.IsRead).HasColumnName("is_read");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            // fast lookup for a user's notification inbox
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });   // a user's notifications page by page, newest first
        });

        mb.Entity<Models.UserContact>(e =>
        {
            e.ToTable("user_contacts");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
        });

        mb.Entity<Models.AppointmentPendingProjection>(e =>
        {
            e.ToTable("appointment_pending_projections");
            e.HasKey(x => x.AppointmentId);
            e.Property(x => x.AppointmentId).HasColumnName("appointment_id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.BookedByUserId).HasColumnName("booked_by_user_id");
            e.Property(x => x.PatientName).HasColumnName("patient_name");

            // fast pending-count lookups per patient/doctor/booker
            e.HasIndex(x => new { x.PatientId, x.Status });
            e.HasIndex(x => new { x.DoctorId, x.Status });
            e.HasIndex(x => new { x.BookedByUserId, x.Status });
        });
    }
}
