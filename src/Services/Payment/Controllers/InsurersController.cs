using System.Security.Claims;
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
public class InsurersController(PaymentDbContext db, ILogger<InsurersController> log) : ControllerBase
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
        log.LogInformation("Insurer {InsurerId} ({InsurerName}) added by admin {AdminId}, active: {IsActive}",
            insurer.Id, insurer.Name, CallerId(), insurer.IsActive);
        return Ok(MapToDto(insurer));
    }

    // Edit a partner, or switch it off (IsActive = false) so patients stop seeing it.
    // One with claims can't be deleted (see Delete): past claims still point at it.
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
        log.LogInformation("Insurer {InsurerId} ({InsurerName}) updated by admin {AdminId}, active: {IsActive}",
            insurer.Id, insurer.Name, CallerId(), insurer.IsActive);
        return Ok(MapToDto(insurer));
    }

    // Delete a partner. One that no claim was made with is removed, and doctors who had ticked it
    // simply stop having it in their list. Claims keep pointing at their insurer, so one that was
    // used is switched off instead: hidden from patients, kept for its claims.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var insurer = await db.Insurers.FindAsync(id);
        if (insurer is null) return NotFound();

        var claims = await db.InsuranceClaims.CountAsync(c => c.InsurerId == id);
        if (claims > 0)
        {
            if (insurer.IsActive)
            {
                insurer.IsActive = false;
                await db.SaveChangesAsync();
            }
            log.LogInformation("Insurer {InsurerId} ({InsurerName}) switched off instead of deleted by admin {AdminId}: {ClaimCount} claim(s) use it",
                insurer.Id, insurer.Name, CallerId(), claims);
            return Ok(new DeleteInsurerResult(false, true, claims));
        }

        var choices = await db.DoctorInsurerChoices.Where(c => c.InsurerIds.Contains(id)).ToListAsync();
        foreach (var choice in choices)
        {
            choice.InsurerIds = choice.InsurerIds.Where(i => i != id).ToList();
            choice.UpdatedAt = DateTime.UtcNow;
        }
        db.Insurers.Remove(insurer);
        await db.SaveChangesAsync();
        log.LogInformation("Insurer {InsurerId} ({InsurerName}) deleted by admin {AdminId}; removed from {DoctorCount} doctor(s)' accepted list",
            insurer.Id, insurer.Name, CallerId(), choices.Count);
        return NoContent();
    }

    // The insurers the calling doctor takes (all of them until they choose)
    [HttpGet("doctor/me")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> GetMyChoice()
    {
        var choice = await db.DoctorInsurerChoices.FindAsync(DoctorId());
        return Ok(choice is null ? new AcceptedInsurersDto(true, []) : new AcceptedInsurersDto(false, choice.InsurerIds));
    }

    // Save the calling doctor's choice. AcceptsAll goes back to the default (no row).
    [HttpPut("doctor/me")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> SaveMyChoice([FromBody] AcceptedInsurersDto req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var doctorId = DoctorId();
        var choice = await db.DoctorInsurerChoices.FindAsync(doctorId);
        if (req.AcceptsAll)
        {
            if (choice is not null) db.DoctorInsurerChoices.Remove(choice);
        }
        else
        {
            // Only real insurers; inactive ones are kept so the choice survives an admin pausing one
            var requested = req.InsurerIds.Distinct().ToList();
            var known = await db.Insurers.Where(i => requested.Contains(i.Id)).Select(i => i.Id).ToListAsync();
            if (choice is null)
            {
                choice = new Models.DoctorInsurerChoice { DoctorId = doctorId };
                db.DoctorInsurerChoices.Add(choice);
            }
            choice.InsurerIds = known;
            choice.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        log.LogInformation("Doctor {DoctorId} now accepts {Insurers}", doctorId,
            req.AcceptsAll ? "every insurer" : $"{choice!.InsurerIds.Count} insurer(s)");
        return Ok(req.AcceptsAll ? new AcceptedInsurersDto(true, []) : new AcceptedInsurersDto(false, choice!.InsurerIds));
    }

    // The active insurers a doctor takes, for the patient's checkout
    [HttpGet("doctor/{doctorId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetForDoctor(Guid doctorId)
    {
        var choice = await db.DoctorInsurerChoices.FindAsync(doctorId);
        var query = db.Insurers.Where(i => i.IsActive);
        if (choice is not null) query = query.Where(i => choice.InsurerIds.Contains(i.Id));

        var insurers = await query.OrderBy(i => i.Name).ToListAsync();
        return Ok(insurers.Select(MapToDto));
    }

    // Case-insensitive, so "SONAS" and "Sonas" can't both exist
    private Task<bool> NameTakenAsync(string name, Guid? exceptId) =>
        db.Insurers.AnyAsync(i => i.Name.ToLower() == name.ToLower() && i.Id != exceptId);

    private string? CallerId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private Guid DoctorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InsurerDto MapToDto(Models.Insurer i) => new(i.Id, i.Name, i.Phone, i.Email, i.IsActive);
}
