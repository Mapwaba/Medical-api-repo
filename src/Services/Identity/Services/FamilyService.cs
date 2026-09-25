using LandaDoc.Identity.Data;
using LandaDoc.Identity.Models;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Services;

public class FamilyService(IdentityDbContext db, IPublishEndpoint bus) : IFamilyService
{
    public async Task<InviteFamilyResult> InviteAsync(Guid requesterId, CreateFamilyInviteRequest req)
    {
        if (!Enum.TryParse<FamilyRelationType>(req.RelationType, true, out var relationType))
            return new InviteFamilyResult(InviteFamilyResultStatus.InvalidRelationType);

        var recipient = await db.Users.FirstOrDefaultAsync(u => u.Email == req.RecipientEmail);
        if (recipient is null)
            return new InviteFamilyResult(InviteFamilyResultStatus.RecipientNotFound);
        if (recipient.Id == requesterId)
            return new InviteFamilyResult(InviteFamilyResultStatus.CannotInviteSelf);

        var alreadyLinked = await db.FamilyLinks.AnyAsync(l =>
            ((l.RequesterUserId == requesterId && l.RecipientUserId == recipient.Id) ||
             (l.RequesterUserId == recipient.Id && l.RecipientUserId == requesterId)) &&
            (l.Status == FamilyLinkStatus.Pending || l.Status == FamilyLinkStatus.Accepted));
        if (alreadyLinked)
            return new InviteFamilyResult(InviteFamilyResultStatus.AlreadyLinked);

        var requester = await db.Users.FirstAsync(u => u.Id == requesterId);

        var link = new FamilyLink
        {
            RequesterUserId = requesterId,
            RecipientUserId = recipient.Id,
            RelationType = relationType,
            RequesterIsParent = relationType == FamilyRelationType.Parent && req.IAmTheParent
        };
        db.FamilyLinks.Add(link);
        await db.SaveChangesAsync();

        return new InviteFamilyResult(InviteFamilyResultStatus.Success, MapToInviteDto(link, requester, recipient));
    }

    public async Task<RespondFamilyInviteResult> AcceptAsync(Guid linkId, Guid callerId)
    {
        var link = await db.FamilyLinks.FirstOrDefaultAsync(l => l.Id == linkId);
        if (link is null) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.NotFound);
        if (link.RecipientUserId != callerId) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Forbidden);
        if (link.Status != FamilyLinkStatus.Pending) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.InvalidStatus);

        link.Status = FamilyLinkStatus.Accepted;
        link.RespondedByUserId = callerId;
        link.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new FamilyLinkAcceptedEvent(link.Id, link.RequesterUserId, link.RecipientUserId, DateTime.UtcNow));

        return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Success);
    }

    public async Task<RespondFamilyInviteResult> DeclineAsync(Guid linkId, Guid callerId)
    {
        var link = await db.FamilyLinks.FirstOrDefaultAsync(l => l.Id == linkId);
        if (link is null) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.NotFound);
        if (link.RecipientUserId != callerId) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Forbidden);
        if (link.Status != FamilyLinkStatus.Pending) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.InvalidStatus);

        link.Status = FamilyLinkStatus.Declined;
        link.RespondedByUserId = callerId;
        link.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        // No event — Appointment never materialized authority for a still-pending link.

        return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Success);
    }

    public async Task<RespondFamilyInviteResult> RevokeAsync(Guid linkId, Guid callerId)
    {
        var link = await db.FamilyLinks.FirstOrDefaultAsync(l => l.Id == linkId);
        if (link is null) return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.NotFound);
        if (link.RequesterUserId != callerId && link.RecipientUserId != callerId)
            return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Forbidden);

        if (link.Status == FamilyLinkStatus.Pending)
        {
            // Only the requester can withdraw their own still-pending invite —
            // the recipient's equivalent action is DeclineAsync.
            if (link.RequesterUserId != callerId)
                return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Forbidden);

            link.Status = FamilyLinkStatus.Revoked;
            link.RespondedByUserId = callerId;
            link.RespondedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            // No event — never became Accepted, Appointment has nothing to retract.
            return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Success);
        }

        if (link.Status != FamilyLinkStatus.Accepted)
            return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.InvalidStatus);

        link.Status = FamilyLinkStatus.Revoked;
        link.RespondedByUserId = callerId;
        link.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new FamilyLinkRevokedEvent(link.Id, link.RequesterUserId, link.RecipientUserId, DateTime.UtcNow));

        return new RespondFamilyInviteResult(RespondFamilyInviteResultStatus.Success);
    }

    public async Task<FamilyOverviewDto> GetMyFamilyAsync(Guid callerId)
    {
        var links = await db.FamilyLinks
            .Where(l => l.RequesterUserId == callerId || l.RecipientUserId == callerId)
            .ToListAsync();

        var caller = await db.Users.FirstAsync(u => u.Id == callerId);
        var otherUserIds = links.Select(l => l.RequesterUserId == callerId ? l.RecipientUserId : l.RequesterUserId)
            .Distinct().ToList();
        var otherUsers = await db.Users.Where(u => otherUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var accepted = links.Where(l => l.Status == FamilyLinkStatus.Accepted).ToList();
        var family = accepted.Select(l =>
        {
            var otherId = l.RequesterUserId == callerId ? l.RecipientUserId : l.RequesterUserId;
            var other = otherUsers[otherId];
            return new FamilyMemberDto(l.Id, other.Id, other.FirstName ?? "", other.LastName ?? "",
                RelationLabelFor(l, callerId, other.Gender));
        }).ToList();

        var pendingSent = links.Where(l => l.Status == FamilyLinkStatus.Pending && l.RequesterUserId == callerId)
            .Select(l => MapToInviteDto(l, caller, otherUsers[l.RecipientUserId]))
            .ToList();
        var pendingReceived = links.Where(l => l.Status == FamilyLinkStatus.Pending && l.RecipientUserId == callerId)
            .Select(l => MapToInviteDto(l, otherUsers[l.RequesterUserId], caller))
            .ToList();

        var dependents = await db.Dependents.Where(d => d.GuardianUserId == callerId).ToListAsync();
        var dependentDtos = dependents.Select(d => new DependentDto(
            d.Id, d.GuardianUserId, d.FirstName, d.LastName, d.DateOfBirth,
            d.Gender?.ToString(), ChildSideLabel(d.Gender), d.CreatedAt)).ToList();

        return new FamilyOverviewDto(family, dependentDtos, pendingSent, pendingReceived);
    }

    public async Task<DependentDto> AddDependentAsync(Guid guardianId, AddDependentRequest req)
    {
        Gender? gender = null;
        if (!string.IsNullOrEmpty(req.Gender) && Enum.TryParse<Gender>(req.Gender, true, out var parsed))
            gender = parsed;

        var dependent = new Dependent
        {
            GuardianUserId = guardianId,
            FirstName = req.FirstName,
            LastName = req.LastName,
            DateOfBirth = req.DateOfBirth,
            Gender = gender
        };
        db.Dependents.Add(dependent);
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new DependentAddedEvent(dependent.Id, dependent.GuardianUserId, DateTime.UtcNow));

        return new DependentDto(dependent.Id, dependent.GuardianUserId, dependent.FirstName, dependent.LastName,
            dependent.DateOfBirth, dependent.Gender?.ToString(), ChildSideLabel(dependent.Gender), dependent.CreatedAt);
    }

    public async Task<RemoveDependentResultStatus> RemoveDependentAsync(Guid dependentId, Guid callerId)
    {
        var dependent = await db.Dependents.FirstOrDefaultAsync(d => d.Id == dependentId);
        if (dependent is null) return RemoveDependentResultStatus.NotFound;
        if (dependent.GuardianUserId != callerId) return RemoveDependentResultStatus.Forbidden;

        db.Dependents.Remove(dependent);
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new DependentRemovedEvent(dependent.Id, dependent.GuardianUserId, DateTime.UtcNow));

        return RemoveDependentResultStatus.Success;
    }

    private static FamilyInviteDto MapToInviteDto(FamilyLink link, User? requester, User? recipient) => new(
        link.Id, link.RequesterUserId, requester is null ? "" : $"{requester.FirstName} {requester.LastName}".Trim(),
        link.RecipientUserId, recipient is null ? "" : $"{recipient.FirstName} {recipient.LastName}".Trim(),
        link.RelationType.ToString(), link.Status.ToString(), link.CreatedAt);

    // "Your <label>" from the caller's point of view, gendered on the OTHER party.
    private static string RelationLabelFor(FamilyLink link, Guid callerId, Gender? otherGender)
    {
        if (link.RelationType == FamilyRelationType.Sibling)
            return SiblingLabel(otherGender);

        var callerIsParent = (callerId == link.RequesterUserId && link.RequesterIsParent) ||
                              (callerId == link.RecipientUserId && !link.RequesterIsParent);
        return callerIsParent ? ChildSideLabel(otherGender) : ParentSideLabel(otherGender);
    }

    private static string SiblingLabel(Gender? g) => g switch
    {
        Gender.Male => "Brother",
        Gender.Female => "Sister",
        _ => "Sibling"
    };

    private static string ParentSideLabel(Gender? g) => g switch
    {
        Gender.Male => "Father",
        Gender.Female => "Mother",
        _ => "Parent"
    };

    private static string ChildSideLabel(Gender? g) => g switch
    {
        Gender.Male => "Son",
        Gender.Female => "Daughter",
        _ => "Child"
    };
}
