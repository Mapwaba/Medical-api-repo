namespace LandaDoc.Shared.Events;

// Published by the Payment service when a booking's payment fails.
// The Appointment service consumes this to auto-cancel the booking.
public record PaymentFailedEvent(
    Guid AppointmentId,
    string? Reason,
    DateTime OccurredAt
);
