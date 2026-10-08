using LandaDoc.Shared.Models;

namespace LandaDoc.Appointment.Models;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RefNumber { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public Guid BookedByUserId { get; set; } // who actually made the booking — self, or an authorized family member
    public Guid? ClinicId { get; set; }
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public string? Motif { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }
    // The patient asked to pay through their insurer and the doctor hasn't reviewed it yet —
    // the unpaid-booking expiry leaves the appointment alone meanwhile.
    public bool AwaitingInsuranceReview { get; set; }
    // While awaiting review: when the doctor gets a reminder (cleared once sent) and when the
    // claim is declined automatically if they still haven't answered. Real UTC times.
    public DateTime? InsuranceReviewRemindAt { get; set; }
    public DateTime? InsuranceReviewDueAt { get; set; }
    // When set, the unpaid booking expires at this time instead of CreatedAt + the usual
    // window (after a declined insurance claim the patient gets longer to pay another way).
    public DateTime? PaymentDueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
