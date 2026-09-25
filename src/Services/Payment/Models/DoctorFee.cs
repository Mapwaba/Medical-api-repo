namespace LandaDoc.Payment.Models;

// Local read-model of "what does this doctor charge", built from DoctorApprovedEvent —
// Payment doesn't own doctor profile data, so it keeps just enough to price a booking.
public class DoctorFee
{
    public Guid DoctorId { get; set; }
    public decimal ConsultationFee { get; set; }
    public decimal PlatformFeePct { get; set; } = 8m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
