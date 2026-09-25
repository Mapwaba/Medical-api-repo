namespace LandaDoc.Shared.Events;

// Published by the Appointment service when a patient reschedules an existing
// appointment. Availability consumes it to invalidate its slot cache for both
// the freed-up old date and the newly-taken date.
public record AppointmentRescheduledEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime OldSlotStart,
    DateTime NewSlotStart,
    DateTime OccurredAt
);
