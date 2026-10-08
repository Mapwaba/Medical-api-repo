using System.Security.Claims;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicsController(IClinicService clinics, ILogger<ClinicsController> log) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll() => Ok((await clinics.GetAllAsync()).Select(MapToDto));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var clinic = await clinics.GetByIdAsync(id);
        return clinic is null ? NotFound() : Ok(MapToDto(clinic));
    }

    // The Doctor app's registration form lets a not-yet-registered doctor pick their clinics,
    // so it needs this list before login. It returns only id, name and city (which patients
    // already see in search results); the full clinic details above still require a login.
    [HttpGet("options")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOptions() =>
        Ok((await clinics.GetAllAsync()).Select(c => new ClinicOptionDto(c.Id, c.Name, c.City)));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateClinicRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var clinic = await clinics.CreateAsync(req);
        log.LogInformation("Clinic {ClinicId} ({ClinicName}, {City}) created by admin {AdminId}",
            clinic.Id, clinic.Name, clinic.City, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return StatusCode(201, MapToDto(clinic));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClinicRequest req)
    {
        var clinic = await clinics.UpdateAsync(id, req);
        if (clinic is not null)
            log.LogInformation("Clinic {ClinicId} ({ClinicName}) updated by admin {AdminId}",
                clinic.Id, clinic.Name, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return clinic is null ? NotFound() : Ok(MapToDto(clinic));
    }

    private static ClinicDto MapToDto(Models.Clinic c) =>
        new(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt);
}
