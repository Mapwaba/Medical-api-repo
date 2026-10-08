using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Data;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    public DbSet<Models.Payment> Payments => Set<Models.Payment>();
    public DbSet<Models.DoctorFee> DoctorFees => Set<Models.DoctorFee>();
    public DbSet<Models.Insurer> Insurers => Set<Models.Insurer>();
    public DbSet<Models.InsuranceClaim> InsuranceClaims => Set<Models.InsuranceClaim>();
    public DbSet<Models.DoctorInsurerChoice> DoctorInsurerChoices => Set<Models.DoctorInsurerChoice>();

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
            e.HasIndex(x => x.ProviderRef).IsUnique().HasFilter("provider_ref IS NOT NULL");
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

        // Partner insurers, managed by admins (names are unique)
        mb.Entity<Models.Insurer>(e =>
        {
            e.ToTable("insurers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Insurance claims: updated in place as they move through review and reconciliation,
        // looked up by appointment (patient's page) and by doctor (doctor's Payments page)
        mb.Entity<Models.InsuranceClaim>(e =>
        {
            e.ToTable("insurance_claims");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PaymentId).HasColumnName("payment_id");
            e.Property(x => x.AppointmentId).HasColumnName("appointment_id");
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.PatientId).HasColumnName("patient_id");
            e.Property(x => x.InsurerId).HasColumnName("insurer_id");
            e.Property(x => x.InsurerName).HasColumnName("insurer_name");
            e.Property(x => x.MemberNumber).HasColumnName("member_number");
            e.Property(x => x.MemberName).HasColumnName("member_name");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(10, 2);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.Note).HasColumnName("note");
            e.Property(x => x.InsurerReference).HasColumnName("insurer_reference");
            e.Property(x => x.AuthorizationReference).HasColumnName("authorization_reference");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.AppointmentId);
            e.HasIndex(x => x.DoctorId);
        });

        // The insurers each doctor takes (one row per doctor who has chosen; see the model)
        mb.Entity<Models.DoctorInsurerChoice>(e =>
        {
            e.ToTable("doctor_insurer_choices");
            e.HasKey(x => x.DoctorId);
            e.Property(x => x.DoctorId).HasColumnName("doctor_id");
            e.Property(x => x.InsurerIds).HasColumnName("insurer_ids");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
