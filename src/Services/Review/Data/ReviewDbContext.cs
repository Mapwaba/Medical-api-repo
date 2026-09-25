using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Review.Data;

public class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options)
{
    public DbSet<Models.Review> Reviews => Set<Models.Review>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Models.Review>(e =>
        {
            e.ToTable("reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AppointmentId).HasColumnName("appointment_id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.Rating).HasColumnName("rating");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            // one review per appointment — doubles as the idempotency/business-rule guard
            e.HasIndex(x => x.AppointmentId).IsUnique();
            e.HasIndex(x => x.DoctorId);
        });
    }
}
