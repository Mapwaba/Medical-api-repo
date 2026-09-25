namespace LandaDoc.Shared.Events;

// Published to RabbitMQ by the Appointment service after a booking is saved.
// Payment, Notification, Search, and Availability all consume it.
public record BookingCreatedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    Guid? ClinicId,
    DateTime SlotStart,
    decimal? ConsultationFee,
    decimal? PlatformFeePct,
    string? PatientEmail,
    string? PatientPhone,
    string? DoctorName,
    string? PatientName,
    DateTime OccurredAt,
    Guid BookedByUserId
);
