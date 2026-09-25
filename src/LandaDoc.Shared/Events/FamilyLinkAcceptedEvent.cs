namespace LandaDoc.Shared.Events;

// Published by the Identity service when a pending family-link invite is accepted.
// Grants bidirectional booking authority — Appointment's consumer materializes
// two rows (Requester can book for Recipient, and vice-versa).
public record FamilyLinkAcceptedEvent(
    Guid LinkId,
    Guid RequesterUserId,
    Guid RecipientUserId,
    DateTime OccurredAt
);
