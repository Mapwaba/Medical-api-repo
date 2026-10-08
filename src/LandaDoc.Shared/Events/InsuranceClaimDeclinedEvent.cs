namespace LandaDoc.Shared.Events;

// Published by the Payment service when the doctor won't accept the patient's insurance
// claim, or (TimedOut) didn't review it before the deadline. The booking stays open for a
// while so the patient can pay another way.
public record InsuranceClaimDeclinedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string? Reason,
    DateTime OccurredAt,
    bool TimedOut = false
);
