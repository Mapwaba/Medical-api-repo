namespace LandaDoc.Shared.Events;

// Published by the Payment service when an admin records money sent to a doctor.
// Notification tells the doctor.
public record DoctorPayoutRecordedEvent(
    Guid PayoutId,
    Guid DoctorId,
    decimal Amount,
    string PaidTo,
    string? Reference,
    DateTime OccurredAt
);
