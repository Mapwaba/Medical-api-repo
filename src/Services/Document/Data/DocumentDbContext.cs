using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Document.Data;

public class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<Models.Document> Documents => Set<Models.Document>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Models.Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.AppointmentId).HasColumnName("appointment_id");
            e.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            e.Property(x => x.FileName).HasColumnName("file_name").IsRequired();
            e.Property(x => x.ContentType).HasColumnName("content_type").IsRequired();
            e.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.StorageKey).HasColumnName("storage_key").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => x.PatientId);
            // fast lookup for the AppointmentCompletedConsumer auto-link scan
            e.HasIndex(x => new { x.PatientId, x.DoctorId, x.AppointmentId });
        });
    }
}
