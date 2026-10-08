namespace LandaDoc.Shared.Events;

// Published by the Payment service when the doctor records that the insurer refused to pay an
// approved claim. The booking stays confirmed; Notification tells the patient they now owe
// the doctor the amount directly.
public record InsuranceClaimRejectedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string InsurerName,
    decimal Amount,
    string? Reason,
    DateTime OccurredAt
);
