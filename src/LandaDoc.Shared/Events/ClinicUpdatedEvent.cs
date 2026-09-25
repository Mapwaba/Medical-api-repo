using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.Events;

// Published by the Admin service whenever a clinic is created or updated.
// The Search service consumes this to refresh the denormalized clinic
// name/city on every doctor profile indexed under that clinic.
public record ClinicUpdatedEvent(
    Guid ClinicId,
    string Name,
    string City,
    string? Address,
    ClinicType Type,
    DateTime OccurredAt
);
