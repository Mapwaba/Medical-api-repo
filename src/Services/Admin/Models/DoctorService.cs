namespace LandaDoc.Admin.Models;

// A service or medical act a doctor offers (e.g. "Ultrasound", 40.00, 30 min), managed on the
// Doctor app's Services page.
public class DoctorService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorProfileId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DoctorProfile DoctorProfile { get; set; } = null!;
}
