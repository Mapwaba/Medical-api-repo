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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DoctorProfile> DoctorProfiles { get; set; } = new List<DoctorProfile>();
}
