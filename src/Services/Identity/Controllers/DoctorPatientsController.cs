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

    // Just the names of everyone on the doctor's appointments, for the agenda, dashboard and
    // payments (the Appointment service only knows patient ids). Unlike the list above it also
    // covers dependants (children etc. booked by a guardian, who have no account of their own)
    // and patients whose appointments were all cancelled, since those still show on the calendar.
    [HttpGet("names")]
    public async Task<IActionResult> GetNames()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var patientIds = await appointments.GetSeenPatientIdsAsync(token, includeCancelled: true);
        if (patientIds is null)
            return Problem("Couldn't load your patients right now. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var accounts = await db.Users
            .Where(u => patientIds.Contains(u.Id) && u.Role == UserRole.Patient)
            .Select(u => new PatientNameDto(u.Id, u.FirstName, u.LastName))
            .ToListAsync();
        var dependents = await db.Dependents
            .Where(d => patientIds.Contains(d.Id))
            .Select(d => new PatientNameDto(d.Id, d.FirstName, d.LastName))
            .ToListAsync();
        return Ok(accounts.Concat(dependents));
    }
}
