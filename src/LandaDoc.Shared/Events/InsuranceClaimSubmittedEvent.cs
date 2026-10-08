namespace LandaDoc.Shared.Events;

// Published by the Payment service when a patient asks to pay through their insurer.
// Appointment holds the booking (no unpaid expiry) until the doctor reviews the claim.
public record InsuranceClaimSubmittedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string InsurerName,
    DateTime OccurredAt
);
