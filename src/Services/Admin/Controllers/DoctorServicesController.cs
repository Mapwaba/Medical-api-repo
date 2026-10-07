using System.Security.Claims;
using LandaDoc.Admin.Data;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Controllers;

// The services and medical acts a doctor offers, managed from the Doctor app's Services page.
[ApiController]
[Route("api/doctors/me/services")]
[Authorize(Roles = "Doctor")]
public class DoctorServicesController(AdminDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var profileId = await MyProfileIdAsync();
        if (profileId is null) return NotFound();

        var services = await db.DoctorServices
            .Where(s => s.DoctorProfileId == profileId)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return Ok(services.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveDoctorServiceRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var profileId = await MyProfileIdAsync();
        if (profileId is null) return NotFound();

        var service = new Models.DoctorService { DoctorProfileId = profileId.Value };
        Apply(service, req);
        db.DoctorServices.Add(service);
        await db.SaveChangesAsync();
        return StatusCode(201, MapToDto(service));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveDoctorServiceRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var service = await FindMineAsync(id);
        if (service is null) return NotFound();

        Apply(service, req);
        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapToDto(service));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var service = await FindMineAsync(id);
        if (service is null) return NotFound();

        db.DoctorServices.Remove(service);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Guid?> MyProfileIdAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.DoctorProfiles.Where(d => d.UserId == userId).Select(d => (Guid?)d.Id).FirstOrDefaultAsync();
    }

    private async Task<Models.DoctorService?> FindMineAsync(Guid id)
    {
        var profileId = await MyProfileIdAsync();
        return profileId is null
            ? null
            : await db.DoctorServices.FirstOrDefaultAsync(s => s.Id == id && s.DoctorProfileId == profileId);
    }

    private static void Apply(Models.DoctorService service, SaveDoctorServiceRequest req)
    {
        service.Name = req.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        service.Price = req.Price;
        service.DurationMinutes = req.DurationMinutes;
    }

    private static DoctorServiceDto MapToDto(Models.DoctorService s) =>
        new(s.Id, s.Name, s.Description, s.Price, s.DurationMinutes);
}
