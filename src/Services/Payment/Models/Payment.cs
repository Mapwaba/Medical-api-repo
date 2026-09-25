using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal NetAmount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    // Set only on the terminal (Completed/Failed) row inserted by a provider's webhook —
    // the Pending row created by BookingCreatedConsumer doesn't know it yet, since the
    // patient picks a provider later, at Initiate time.
    public PaymentProvider? Provider { get; set; }
    public string? ProviderRef { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
