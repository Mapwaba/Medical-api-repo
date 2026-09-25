namespace LandaDoc.Shared.Events;

// Published by the Review service when a patient submits a review.
// The Search service consumes this to update the doctor's running average rating.
public record ReviewSubmittedEvent(
    Guid ReviewId,
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    int Rating,
    DateTime OccurredAt
);
