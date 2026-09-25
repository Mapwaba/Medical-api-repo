namespace LandaDoc.Shared.Events;

// Published by the Payment service when a Stripe payment succeeds.
public record PaymentCompletedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime OccurredAt
);
