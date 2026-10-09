namespace LandaDoc.Shared.Events;

// Published by the Identity service when a guardian registers a dependent
// (a minor/relative with no login of their own). Grants the guardian
// one-directional booking authority over the dependent.
// DependentFirstName lets the guardian's notifications say who a booking is for.
public record DependentAddedEvent(
    Guid DependentId,
    Guid GuardianUserId,
    DateTime OccurredAt,
    string? DependentFirstName = null
);
