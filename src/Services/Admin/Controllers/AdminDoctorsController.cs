using System.Security.Claims;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/admin/doctors")]
[Authorize(Roles = "Admin")]
public class AdminDoctorsController(IDoctorProfileService doctors, ILogger<AdminDoctorsController> log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DoctorApprovalStatus? status) =>
        Ok((await doctors.GetAllAsync(status)).Select(MapToDto));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var profile = await doctors.GetByIdAsync(id);
        return profile is null ? NotFound() : Ok(MapToDto(profile));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDoctorProfileRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await doctors.CreateAsync(req, autoApprove: true);
        if (result.Status == DoctorProfileResultStatus.Success)
            log.LogInformation("Doctor profile {DoctorProfileId} created and approved by admin {AdminId} for account {UserId}",
                result.Profile!.Id, CallerId(), req.UserId);
        return result.Status switch
        {
            DoctorProfileResultStatus.AlreadyExists => Conflict(new { error = "A profile already exists for this user" }),
            DoctorProfileResultStatus.NameTaken => Conflict(new { error = "A doctor with this first and last name already exists", code = "name" }),
            DoctorProfileResultStatus.Success => StatusCode(201, MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    // An admin edits any doctor's profile: same fields and rules as the doctor editing their own
    // (null fields stay as they are, duplicate names are refused, live doctors are re-synced to
    // Search and Payment). Name and phone also live on the doctor's account — the Admin app
    // saves those through Identity's PUT api/auth/admin/users/{userId}.
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOwnDoctorProfileRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var profile = await doctors.GetByIdAsync(id);
        if (profile is null) return NotFound();
        var before = MapToDto(profile);   // snapshot: the service updates this same tracked object

        var result = await doctors.UpdateOwnAsync(profile.UserId, req);
        if (result.Status == DoctorProfileResultStatus.Success)
            log.LogInformation("Doctor profile {DoctorProfileId} edited by admin {AdminId}; changed: {ChangedFields}",
                id, CallerId(), ChangedFields(before, MapToDto(result.Profile!)));
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.NameTaken => Conflict(new { error = "A doctor with this first and last name already exists", code = "name" }),
            DoctorProfileResultStatus.Success => Ok(MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await doctors.ApproveAsync(id);
        if (result.Status == DoctorProfileResultStatus.Success)
            log.LogInformation("Doctor profile {DoctorProfileId} approved by admin {AdminId}", id, CallerId());
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.InvalidTransition => Conflict(new { error = "Doctor cannot be approved from its current status" }),
            DoctorProfileResultStatus.Success => Ok(MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    [HttpPatch("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendDoctorRequest req)
    {
        var result = await doctors.SuspendAsync(id, req);
        if (result.Status == DoctorProfileResultStatus.Success)
            log.LogInformation("Doctor profile {DoctorProfileId} suspended by admin {AdminId}", id, CallerId());
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.InvalidTransition => Conflict(new { error = "Doctor cannot be suspended from its current status" }),
            DoctorProfileResultStatus.Success => Ok(MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    private string? CallerId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Names of the profile fields that differ — logged instead of the values themselves,
    // which can include free text (the bio) that doesn't belong in logs
    internal static List<string> ChangedFields(DoctorProfileDto before, DoctorProfileDto after)
    {
        var changed = new List<string>();
        if (before.FirstName != after.FirstName) changed.Add("FirstName");
        if (before.LastName != after.LastName) changed.Add("LastName");
        if (before.Specialty != after.Specialty) changed.Add("Specialty");
        if (before.LicenseNumber != after.LicenseNumber) changed.Add("LicenseNumber");
        if (before.Bio != after.Bio) changed.Add("Bio");
        if (before.ConsultationFee != after.ConsultationFee) changed.Add("ConsultationFee");
        if (!before.Clinics.Select(c => c.Id).OrderBy(x => x).SequenceEqual(after.Clinics.Select(c => c.Id).OrderBy(x => x)))
            changed.Add("Clinics");
        return changed;
    }

    internal static DoctorProfileDto MapToDto(Models.DoctorProfile d) => new(
        d.Id, d.UserId, d.FirstName, d.LastName, d.Specialty, d.Bio, d.LicenseNumber,
        d.ConsultationFee,
        d.Clinics.Select(c => new ClinicDto(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt)).ToList(),
        d.Status, d.CreatedAt);
}
