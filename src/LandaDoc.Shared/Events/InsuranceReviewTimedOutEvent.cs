namespace LandaDoc.Shared.Events;

// Published by the Appointment service when the doctor hasn't reviewed an insurance claim by
// its deadline. Payment then declines the claim (Expired), which frees the patient to pay
// another way.
public record InsuranceReviewTimedOutEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime OccurredAt
);
