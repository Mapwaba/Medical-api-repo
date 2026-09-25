namespace LandaDoc.Shared.Events;

// Published by the Admin service when a doctor profile is suspended.
// The Identity service consumes this to flip User.IsActive to false;
// the Search service consumes it to remove the doctor from search results.
public record DoctorSuspendedEvent(
    Guid DoctorProfileId,
    Guid UserId,
    string? Reason,
    DateTime OccurredAt
);
