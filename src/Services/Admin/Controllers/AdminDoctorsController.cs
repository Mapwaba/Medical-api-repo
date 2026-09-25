using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/admin/doctors")]
[Authorize(Roles = "Admin")]
public class AdminDoctorsController(IDoctorProfileService doctors) : ControllerBase
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
        return result.Status switch
        {
            DoctorProfileResultStatus.AlreadyExists => Conflict(new { error = "A profile already exists for this user" }),
            DoctorProfileResultStatus.Success => StatusCode(201, MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await doctors.ApproveAsync(id);
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
        return result.Status switch
        {
            DoctorProfileResultStatus.NotFound => NotFound(),
            DoctorProfileResultStatus.InvalidTransition => Conflict(new { error = "Doctor cannot be suspended from its current status" }),
            DoctorProfileResultStatus.Success => Ok(MapToDto(result.Profile!)),
            _ => Problem()
        };
    }

    internal static DoctorProfileDto MapToDto(Models.DoctorProfile d) => new(
        d.Id, d.UserId, d.FirstName, d.LastName, d.Specialty, d.Bio, d.LicenseNumber,
        d.ConsultationFee,
        d.Clinics.Select(c => new ClinicDto(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt)).ToList(),
        d.Status, d.CreatedAt);
}
