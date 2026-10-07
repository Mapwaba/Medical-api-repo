using LandaDoc.Identity.Data;
using LandaDoc.Identity.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Controllers;

// Contact details of the patients a doctor has had a (non-cancelled) appointment with — and no one
// else's. Used by the Doctor app's Patients page and patient file.
[ApiController]
[Route("api/patients/mine")]
[Authorize(Roles = "Doctor")]
public class DoctorPatientsController(IdentityDbContext db, IAppointmentPatientsClient appointments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var patientIds = await appointments.GetSeenPatientIdsAsync(token);
        if (patientIds is null)
            return Problem("Couldn't load your patients right now. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var patients = await db.Users
            .Where(u => patientIds.Contains(u.Id) && u.Role == UserRole.Patient)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        return Ok(patients.Select(u => new PatientContactDto(
            u.Id, u.FirstName, u.LastName, u.Email, u.Phone, u.DateOfBirth, u.Gender?.ToString())));
    }
}
