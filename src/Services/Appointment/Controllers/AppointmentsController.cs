using System.Security.Claims;
using LandaDoc.Appointment.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Appointment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await appointments.CreateAsync(callerId, req);
        return result.Status switch
        {
            CreateAppointmentResultStatus.Forbidden => Forbid(),
            CreateAppointmentResultStatus.SlotConflict => Conflict(new { error = "Slot no longer available" }),
            CreateAppointmentResultStatus.Success => StatusCode(201, result.Appointment),
            _ => Problem()
        };
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        var mine = await appointments.GetMineAsync(callerId, role);
        return Ok(mine.Select(MapToDto));
    }

    [HttpGet("stats/by-doctor")]
    public async Task<IActionResult> StatsByDoctor() => Ok(await appointments.GetStatsByDoctorAsync());

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var appt = await appointments.GetByIdAsync(id);
        return appt is null ? NotFound() : Ok(MapToDto(appt));
    }

    [HttpPatch("{id:guid}/complete")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var doctorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await appointments.CompleteAsync(id, doctorId);
        return result.Status switch
        {
            CompleteAppointmentResultStatus.NotFound => NotFound(),
            CompleteAppointmentResultStatus.Forbidden => Forbid(),
            CompleteAppointmentResultStatus.InvalidStatus => Conflict(new { error = "Appointment cannot be completed from its current status" }),
            CompleteAppointmentResultStatus.Success => Ok(MapToDto(result.Appointment!)),
            _ => Problem()
        };
    }

    [HttpPatch("{id:guid}/reschedule")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var patientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await appointments.RescheduleAsync(id, patientId, req);
        return result.Status switch
        {
            RescheduleAppointmentResultStatus.NotFound => NotFound(),
            RescheduleAppointmentResultStatus.Forbidden => Forbid(),
            RescheduleAppointmentResultStatus.InvalidStatus => Conflict(new { error = "Appointment cannot be rescheduled from its current status" }),
            RescheduleAppointmentResultStatus.SlotConflict => Conflict(new { error = "That slot is no longer available" }),
            RescheduleAppointmentResultStatus.Success => Ok(MapToDto(result.Appointment!)),
            _ => Problem()
        };
    }

    private static AppointmentDto MapToDto(Models.Appointment a) => new(
        a.Id, a.RefNumber, a.DoctorId, a.PatientId, a.ClinicId, a.SlotStart, a.SlotEnd,
        a.Motif, a.Status, a.Notes, null, null, null, null, a.CreatedAt);
}
