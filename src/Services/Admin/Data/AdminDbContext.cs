using LandaDoc.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Clinic ───────────────────────────────────────────────────────
        mb.Entity<Clinic>(e =>
        {
            e.ToTable("clinics");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasConversion<string>();
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.City).HasColumnName("city").IsRequired();
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── DoctorProfile ────────────────────────────────────────────────
        mb.Entity<DoctorProfile>(e =>
        {
            e.ToTable("doctor_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.FirstName).HasColumnName("first_name").IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").IsRequired();
            e.Property(x => x.Specialty).HasColumnName("specialty").IsRequired();
            e.Property(x => x.Bio).HasColumnName("bio");
            e.Property(x => x.LicenseNumber).HasColumnName("license_number");
            e.Property(x => x.ConsultationFee).HasColumnName("consultation_fee").HasPrecision(10, 2);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasMany(x => x.Clinics)
                .WithMany(c => c.DoctorProfiles)
                .UsingEntity(j => j.ToTable("doctor_profile_clinics"));

            e.HasIndex(x => x.UserId).IsUnique(); // one profile per doctor user
            e.HasIndex(x => x.Status);
        });
    }
}
