namespace LandaDoc.Shared.Events;

// Published by Appointment's expiry sweep (PendingPaymentExpiryService) when a booking is
// still unpaid after the configured grace period — a Pending appointment otherwise blocks
// its slot indefinitely, since only a payment webhook would normally resolve its status.
public record BookingExpiredEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime OccurredAt
);
