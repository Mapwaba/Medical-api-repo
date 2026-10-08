using LandaDoc.Payment.Data;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Controllers;

// Partner insurers / medical aids. Admins manage the list; everyone signed in can read the
// active ones (patients choose from them at checkout).
[ApiController]
[Route("api/[controller]")]
public class InsurersController(PaymentDbContext db) : ControllerBase
{
    // Admins get every insurer (to manage the list); everyone else only the active ones
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var query = db.Insurers.AsQueryable();
        if (!User.IsInRole("Admin")) query = query.Where(i => i.IsActive);

        var insurers = await query.OrderBy(i => i.Name).ToListAsync();
        return Ok(insurers.Select(MapToDto));
    }

    // Add a partner. 409 if another insurer already has this name (ignoring case).
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] SaveInsurerRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var name = req.Name.Trim();
        if (await NameTakenAsync(name, null))
            return Conflict(new { error = "An insurer with this name already exists" });

        var insurer = new Models.Insurer
        {
            Name = name,
            Phone = Clean(req.Phone),
            Email = Clean(req.Email),
            IsActive = req.IsActive,
        };
        db.Insurers.Add(insurer);
        await db.SaveChangesAsync();
        return Ok(MapToDto(insurer));
    }

    // Edit a partner, or switch it off (IsActive = false) so patients stop seeing it.
    // Insurers are never deleted: past claims still point at them.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveInsurerRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var insurer = await db.Insurers.FindAsync(id);
        if (insurer is null) return NotFound();

        var name = req.Name.Trim();
        if (await NameTakenAsync(name, id))
            return Conflict(new { error = "An insurer with this name already exists" });

        // Existing claims keep the name they were made under (InsuranceClaim.InsurerName)
        insurer.Name = name;
        insurer.Phone = Clean(req.Phone);
        insurer.Email = Clean(req.Email);
        insurer.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return Ok(MapToDto(insurer));
    }

    // Case-insensitive, so "SONAS" and "Sonas" can't both exist
    private Task<bool> NameTakenAsync(string name, Guid? exceptId) =>
        db.Insurers.AnyAsync(i => i.Name.ToLower() == name.ToLower() && i.Id != exceptId);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InsurerDto MapToDto(Models.Insurer i) => new(i.Id, i.Name, i.Phone, i.Email, i.IsActive);
}
