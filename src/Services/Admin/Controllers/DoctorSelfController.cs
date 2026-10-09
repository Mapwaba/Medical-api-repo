using System.Security.Claims;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/doctors/me")]
[Authorize(Roles = "Doctor")]
public class DoctorSelfController(IDoctorProfileService doctors, IConfiguration config, ILogger<DoctorSelfController> log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await doctors.GetByUserIdAsync(callerId);
        return profile is null ? NotFound() : Ok(AdminDoctorsController.MapToDto(profile));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOwnDoctorProfileRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        // Patients choose their doctor partly by face: a doctor can't set up a profile without a photo
        if (req.PhotoDataUrl is null) return BadRequest(PhotoRequired());
        if (AdminDoctorsController.BadPhoto(req.PhotoDataUrl)) return BadRequest(AdminDoctorsController.PhotoError());
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var fullReq = new CreateDoctorProfileRequest(
            callerId, req.FirstName, req.LastName, req.Specialty,
            req.Bio, req.LicenseNumber, req.ClinicIds, req.ConsultationFee, req.PhotoDataUrl);

        // Self-registered doctors wait as Pending until an admin approves them on the
        // Admin Doctors page. Doctors__RequireApproval=false makes them go live straight away.
        var autoApprove = !config.GetValue("Doctors:RequireApproval", true);
        var result = await doctors.CreateAsync(fullReq, autoApprove);
        if (result.Status == DoctorProfileResultStatus.Success)
            log.LogInformation("Doctor {UserId} created their profile {DoctorProfileId} ({Status})",
                callerId, result.Profile!.Id, result.Profile.Status);
        return result.Status switch
        {
            DoctorProfileResultStatus.AlreadyExists => Conflict(new { error = "You already have a profile" }),
            DoctorProfileResultStatus.NameTaken => Conflict(new { error = "A doctor with this first and last name already exists", code = "name" }),
            DoctorProfileResultStatus.Success => StatusCode(201, AdminDoctorsController.MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateOwnDoctorProfileRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (AdminDoctorsController.BadPhoto(req.PhotoDataUrl)) return BadRequest(AdminDoctorsController.PhotoError());
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var existing = await doctors.GetByUserIdAsync(callerId);
        var before = existing is null ? null : AdminDoctorsController.MapToDto(existing);   // snapshot before the edit

        // A doctor from before photos were required adds one the next time they save their profile
        if (existing is not null && existing.PhotoDataUrl is null && req.PhotoDataUrl is null)
            return BadRequest(PhotoRequired());

        var result = await doctors.UpdateOwnAsync(callerId, req);
        if (result.Status == DoctorProfileResultStatus.Success && before is not null)
            log.LogInformation("Doctor {UserId} edited their profile {DoctorProfileId}; changed: {ChangedFields}",
                callerId, before.Id, AdminDoctorsController.ChangedFields(before, AdminDoctorsController.MapToDto(result.Profile!)));
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.NameTaken => Conflict(new { error = "A doctor with this first and last name already exists", code = "name" }),
            DoctorProfileResultStatus.Success => Ok(AdminDoctorsController.MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    private static object PhotoRequired() => new { error = "Add a photo of yourself to your profile", code = "photo_required" };
}
