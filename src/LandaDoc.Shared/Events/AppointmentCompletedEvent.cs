namespace LandaDoc.Shared.Events;

// Published by the Appointment service when a doctor marks an appointment complete.
// The Document service consumes this to auto-link any unlinked patient documents.
public record AppointmentCompletedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime CompletedAt,
    DateTime OccurredAt
);
