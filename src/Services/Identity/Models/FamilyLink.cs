using LandaDoc.Shared.Models;
namespace LandaDoc.Identity.Models;

public class FamilyLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public FamilyRelationType RelationType { get; set; }
    // Only meaningful when RelationType == Parent (Sibling is symmetric):
    // true => Requester is the parent, Recipient is the child.
    public bool RequesterIsParent { get; set; }
    public FamilyLinkStatus Status { get; set; } = FamilyLinkStatus.Pending;
    public Guid? RespondedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
