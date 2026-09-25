namespace LandaDoc.Shared.Events;

// Published by the Identity service when a guardian removes a dependent.
public record DependentRemovedEvent(
    Guid DependentId,
    Guid GuardianUserId,
    DateTime OccurredAt
);
