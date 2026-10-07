using System.Security.Claims;
using LandaDoc.Admin.Data;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Controllers;

// The establishments a doctor works at, editable by that doctor from the Doctor app's
// Workplace page. A doctor can only see and change clinics linked to their own profile.
[ApiController]
[Route("api/doctors/me/workplaces")]
[Authorize(Roles = "Doctor")]
public class DoctorWorkplaceController(AdminDbContext db, IClinicService clinics) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var profile = await MyProfileAsync();
        if (profile is null) return NotFound();
        return Ok(profile.Clinics.OrderBy(c => c.Name).Select(MapToDto));
    }

    [HttpPut("{clinicId:guid}")]
    public async Task<IActionResult> Update(Guid clinicId, [FromBody] UpdateWorkplaceRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (req.LogoDataUrl is not null && !req.LogoDataUrl.StartsWith("data:image/", StringComparison.Ordinal))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(req.LogoDataUrl)] = ["The logo must be an image."]
            }));

        var profile = await MyProfileAsync();
        var clinic = profile?.Clinics.FirstOrDefault(c => c.Id == clinicId);
        if (clinic is null) return NotFound();

        clinic.Address = Blank(req.Address);
        clinic.Phone = Blank(req.Phone);
        clinic.Email = Blank(req.Email);
        clinic.Website = Blank(req.Website);
        clinic.Description = Blank(req.Description);
        clinic.LogoDataUrl = Blank(req.LogoDataUrl);

        // Goes through the clinic service (same tracked entity) so name/city/address changes are
        // saved together with the fields above and published to Search like an admin edit.
        var updated = await clinics.UpdateAsync(clinic.Id, new UpdateClinicRequest(
            req.Name.Trim(), null, clinic.Address, req.City.Trim(), clinic.Phone));
        return Ok(MapToDto(updated!));
    }

    private async Task<Models.DoctorProfile?> MyProfileAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.DoctorProfiles.Include(d => d.Clinics).FirstOrDefaultAsync(d => d.UserId == userId);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WorkplaceDto MapToDto(Models.Clinic c) =>
        new(c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.Email, c.Website, c.Description, c.LogoDataUrl);
}
