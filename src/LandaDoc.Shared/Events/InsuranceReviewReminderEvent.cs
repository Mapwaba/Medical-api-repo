namespace LandaDoc.Shared.Events;

// Published by the Appointment service when an insurance claim has waited half its review
// time without an answer. Notification reminds the doctor.
public record InsuranceReviewReminderEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime DueAt,
    DateTime OccurredAt
);
