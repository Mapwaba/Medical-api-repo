using LandaDoc.Shared.Models;

namespace LandaDoc.Admin.Models;

public class Clinic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ClinicType Type { get; set; } = ClinicType.PrivatePractice;
    public string? Address { get; set; }
    public string City { get; set; } = "";
    public string? Phone { get; set; }
    // Details a doctor fills in on the Doctor app's Workplace page
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? LogoDataUrl { get; set; } // small image stored inline as a data: URL
    // Offered to doctors in the clinic lists they pick from. A clinic switched off stays with the
    // doctors who already work there (and patients still see it on them).
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DoctorProfile> DoctorProfiles { get; set; } = new List<DoctorProfile>();
}
