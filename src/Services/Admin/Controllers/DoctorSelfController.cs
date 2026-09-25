using System.Security.Claims;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/doctors/me")]
[Authorize(Roles = "Doctor")]
public class DoctorSelfController(IDoctorProfileService doctors) : ControllerBase
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
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var fullReq = new CreateDoctorProfileRequest(
            callerId, req.FirstName, req.LastName, req.Specialty,
            req.Bio, req.LicenseNumber, req.ClinicIds, req.ConsultationFee);

        var result = await doctors.CreateAsync(fullReq);
        return result.Status switch
        {
            DoctorProfileResultStatus.AlreadyExists => Conflict(new { error = "You already have a profile" }),
            DoctorProfileResultStatus.Success => StatusCode(201, AdminDoctorsController.MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateOwnDoctorProfileRequest req)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await doctors.UpdateOwnAsync(callerId, req);
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.Success => Ok(AdminDoctorsController.MapToDto(result.Profile!)),
            _ => Problem()
        };
    }
}
