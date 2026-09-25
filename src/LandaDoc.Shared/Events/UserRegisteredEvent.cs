namespace LandaDoc.Shared.Events;

// Published by the Identity service after a new user is created.
// Notification service sends the welcome email; other services can build
// their own read-model projection from it.
public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string Role,
    string FirstName,
    string LastName,
    string? Phone,
    DateTime OccurredAt
);
