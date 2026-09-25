using System.ComponentModel.DataAnnotations;
namespace LandaDoc.Shared.DTOs;

public record CreateFamilyInviteRequest(
    [Required, EmailAddress] string RecipientEmail,
    [Required] string RelationType, // "Parent" or "Sibling"
    bool IAmTheParent); // only read when RelationType == "Parent"

public record FamilyInviteDto(
    Guid Id, Guid RequesterUserId, string RequesterName,
    Guid RecipientUserId, string RecipientName,
    string RelationType, string Status, DateTime CreatedAt);

public record FamilyMemberDto(Guid LinkId, Guid UserId, string FirstName, string LastName, string RelationLabel);

public record AddDependentRequest(
    [Required] string FirstName, [Required] string LastName,
    [Required] DateOnly DateOfBirth, string? Gender);

public record DependentDto(Guid Id, Guid GuardianUserId, string FirstName, string LastName,
    DateOnly DateOfBirth, string? Gender, string RelationLabel, DateTime CreatedAt);

public record FamilyOverviewDto(
    List<FamilyMemberDto> Family, List<DependentDto> Dependents,
    List<FamilyInviteDto> PendingSent, List<FamilyInviteDto> PendingReceived);
