namespace LandaDoc.Shared.Events;

// Published by the Identity service when a previously Accepted family link is
// revoked by either party. Appointment's consumer deletes the authorization
// rows it materialized on FamilyLinkAcceptedEvent.
public record FamilyLinkRevokedEvent(
    Guid LinkId,
    Guid RequesterUserId,
    Guid RecipientUserId,
    DateTime OccurredAt
);
