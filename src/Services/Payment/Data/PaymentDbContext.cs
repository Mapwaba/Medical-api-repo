using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Data;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    public DbSet<Models.Payment> Payments => Set<Models.Payment>();
    public DbSet<Models.DoctorFee> DoctorFees => Set<Models.DoctorFee>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Models.Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AppointmentId).HasColumnName("appointment_id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasPrecision(10, 2);
            e.Property(x => x.PlatformFee).HasColumnName("platform_fee").HasPrecision(10, 2);
            e.Property(x => x.NetAmount).HasColumnName("net_amount").HasPrecision(10, 2);
            e.Property(x => x.Status).HasColumnName("status")
                .HasConversion<string>();
            e.Property(x => x.Provider).HasColumnName("provider")
                .HasConversion<string>();
            e.Property(x => x.ProviderRef).HasColumnName("provider_ref");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            // lets the consumer/webhook check for an existing row before inserting
            e.HasIndex(x => x.AppointmentId);
            e.HasIndex(x => x.ProviderRef).IsUnique().HasFilter("[provider_ref] IS NOT NULL");
        });

        mb.Entity<Models.DoctorFee>(e =>
        {
            e.ToTable("doctor_fees");
            e.HasKey(x => x.DoctorId);
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.ConsultationFee).HasColumnName("consultation_fee").HasPrecision(10, 2);
            e.Property(x => x.PlatformFeePct).HasColumnName("platform_fee_pct").HasPrecision(5, 2);
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
