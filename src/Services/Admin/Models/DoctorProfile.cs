using LandaDoc.Shared.Models;

namespace LandaDoc.Admin.Models;

public class DoctorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; } // Identity.User.Id
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string? Bio { get; set; }
    public string? LicenseNumber { get; set; }
    public decimal ConsultationFee { get; set; }
    public string? PhotoDataUrl { get; set; } // profile picture, stored inline as a data: URL (ProfilePhoto)
    public DoctorApprovalStatus Status { get; set; } = DoctorApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Clinic> Clinics { get; set; } = new List<Clinic>();
    public ICollection<DoctorService> Services { get; set; } = new List<DoctorService>();
}
