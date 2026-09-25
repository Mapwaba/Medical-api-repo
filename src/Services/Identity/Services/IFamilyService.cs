using LandaDoc.Shared.DTOs;
namespace LandaDoc.Identity.Services;

public enum InviteFamilyResultStatus { Success, RecipientNotFound, CannotInviteSelf, AlreadyLinked, InvalidRelationType }
public record InviteFamilyResult(InviteFamilyResultStatus Status, FamilyInviteDto? Invite = null);

public enum RespondFamilyInviteResultStatus { Success, NotFound, Forbidden, InvalidStatus }
public record RespondFamilyInviteResult(RespondFamilyInviteResultStatus Status);

public enum RemoveDependentResultStatus { Success, NotFound, Forbidden }

public interface IFamilyService
{
    Task<InviteFamilyResult> InviteAsync(Guid requesterId, CreateFamilyInviteRequest req);
    Task<RespondFamilyInviteResult> AcceptAsync(Guid linkId, Guid callerId);
    Task<RespondFamilyInviteResult> DeclineAsync(Guid linkId, Guid callerId);
    Task<RespondFamilyInviteResult> RevokeAsync(Guid linkId, Guid callerId);
    Task<FamilyOverviewDto> GetMyFamilyAsync(Guid callerId);
    Task<DependentDto> AddDependentAsync(Guid guardianId, AddDependentRequest req);
    Task<RemoveDependentResultStatus> RemoveDependentAsync(Guid dependentId, Guid callerId);
}
