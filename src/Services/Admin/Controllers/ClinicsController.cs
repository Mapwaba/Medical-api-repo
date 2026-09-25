using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Admin.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicsController(IClinicService clinics) : ControllerBase
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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateClinicRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var clinic = await clinics.CreateAsync(req);
        return StatusCode(201, MapToDto(clinic));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClinicRequest req)
    {
        var clinic = await clinics.UpdateAsync(id, req);
        return clinic is null ? NotFound() : Ok(MapToDto(clinic));
    }

    private static ClinicDto MapToDto(Models.Clinic c) =>
        new(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt);
}
