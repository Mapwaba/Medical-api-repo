using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.Events;

// Published by the Payment service when a booking is paid — or, for Provider = Insurance,
// when the doctor accepts the patient's insurance claim (the insurer pays later).
public record PaymentCompletedEvent(
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTime OccurredAt,
    PaymentProvider? Provider = null
);
