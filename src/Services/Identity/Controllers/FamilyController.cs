using System.Security.Claims;
using LandaDoc.Identity.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Identity.Controllers;

[ApiController, Route("api/family")]
[Authorize(Roles = "Patient")]
public class FamilyController(IFamilyService family) : ControllerBase
{
    [HttpPost("invites")]
    public async Task<IActionResult> Invite([FromBody] CreateFamilyInviteRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var requesterId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await family.InviteAsync(requesterId, req);
        return result.Status switch
        {
            InviteFamilyResultStatus.InvalidRelationType => BadRequest(new { error = "RelationType must be Parent or Sibling" }),
            InviteFamilyResultStatus.RecipientNotFound => NotFound(new { error = "No account found for that email" }),
            InviteFamilyResultStatus.CannotInviteSelf => BadRequest(new { error = "You cannot invite yourself" }),
            InviteFamilyResultStatus.AlreadyLinked => Conflict(new { error = "A family link already exists between you and this person" }),
            InviteFamilyResultStatus.Success => StatusCode(201, result.Invite),
            _ => Problem()
        };
    }

    [HttpPost("invites/{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id) => RespondAsync(id, family.AcceptAsync);

    [HttpPost("invites/{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id) => RespondAsync(id, family.DeclineAsync);

    [HttpPost("invites/{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id) => RespondAsync(id, family.RevokeAsync);

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await family.GetMyFamilyAsync(callerId));
    }

    [HttpPost("dependents")]
    public async Task<IActionResult> AddDependent([FromBody] AddDependentRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var guardianId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var dependent = await family.AddDependentAsync(guardianId, req);
        return StatusCode(201, dependent);
    }

    [HttpDelete("dependents/{id:guid}")]
    public async Task<IActionResult> RemoveDependent(Guid id)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var status = await family.RemoveDependentAsync(id, callerId);
        return status switch
        {
            RemoveDependentResultStatus.NotFound => NotFound(),
            RemoveDependentResultStatus.Forbidden => Forbid(),
            RemoveDependentResultStatus.Success => NoContent(),
            _ => Problem()
        };
    }

    private async Task<IActionResult> RespondAsync(Guid linkId, Func<Guid, Guid, Task<RespondFamilyInviteResult>> respond)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await respond(linkId, callerId);
        return result.Status switch
        {
            RespondFamilyInviteResultStatus.NotFound => NotFound(),
            RespondFamilyInviteResultStatus.Forbidden => Forbid(),
            RespondFamilyInviteResultStatus.InvalidStatus => Conflict(new { error = "Invite is not in a respondable state" }),
            RespondFamilyInviteResultStatus.Success => NoContent(),
            _ => Problem()
        };
    }
}
