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
    // Only clinics still offered to doctors (a registering doctor can't have chosen a switched-off one)
    [HttpGet("options")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOptions() =>
        Ok((await clinics.GetAllAsync()).Where(c => c.IsActive).Select(c => new ClinicOptionDto(c.Id, c.Name, c.City)));

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

    // Removes a clinic no doctor works at; one with doctors is switched off instead (200 with
    // SwitchedOff), so it leaves the lists doctors pick from but stays on their profiles
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await clinics.DeleteAsync(id);
        if (result is null) return NotFound();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (result.Deleted)
        {
            log.LogInformation("Clinic {ClinicId} deleted by admin {AdminId}", id, adminId);
            return NoContent();
        }
        log.LogInformation("Clinic {ClinicId} switched off instead of deleted by admin {AdminId}: {DoctorCount} doctor(s) work there",
            id, adminId, result.Doctors);
        return Ok(result);
    }

    private static ClinicDto MapToDto(Models.Clinic c) =>
        new(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt, c.IsActive);
}
