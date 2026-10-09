using System.Security.Claims;
using LandaDoc.Appointment.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Appointment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService appointments, ILogger<AppointmentsController> log) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await appointments.CreateAsync(callerId, req);
        if (result.Status == CreateAppointmentResultStatus.Success)
        {
            var a = result.Appointment!;
            log.LogInformation("Appointment {AppointmentId} (ref {RefNumber}) booked by {UserId} for patient {PatientId} with doctor {DoctorId} at {SlotStart}",
                a.Id, a.RefNumber, callerId, a.PatientId, a.DoctorId, a.SlotStart);
        }
        else
        {
            log.LogInformation("Booking by {UserId} with doctor {DoctorId} at {SlotStart} refused: {Reason}",
                callerId, req.DoctorId, req.SlotStart, result.Status);
        }
        return result.Status switch
        {
            CreateAppointmentResultStatus.Forbidden => Forbid(),
            CreateAppointmentResultStatus.SlotConflict => Conflict(new { error = "Slot no longer available" }),
            // 409 like a taken slot, so the apps reload the times and the started one disappears
            CreateAppointmentResultStatus.SlotInPast => Conflict(new { error = "That time has already passed", code = "slot_past" }),
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
        if (appt is null) return NotFound();

        // Only the people involved: the patient, their doctor, or the family member who booked it
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (callerId != appt.PatientId && callerId != appt.DoctorId && callerId != appt.BookedByUserId)
            return Forbid();

        return Ok(MapToDto(appt));
    }

    [HttpPatch("{id:guid}/complete")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var doctorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await appointments.CompleteAsync(id, doctorId);
        if (result.Status == CompleteAppointmentResultStatus.Success)
            log.LogInformation("Appointment {AppointmentId} marked completed by doctor {DoctorId}", id, doctorId);
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
        if (result.Status == RescheduleAppointmentResultStatus.Success)
            log.LogInformation("Appointment {AppointmentId} rescheduled by {UserId} to {SlotStart}", id, patientId, req.SlotStart);
        else
            log.LogInformation("Reschedule of appointment {AppointmentId} by {UserId} to {SlotStart} refused: {Reason}",
                id, patientId, req.SlotStart, result.Status);
        return result.Status switch
        {
            RescheduleAppointmentResultStatus.NotFound => NotFound(),
            RescheduleAppointmentResultStatus.Forbidden => Forbid(),
            RescheduleAppointmentResultStatus.InvalidStatus => Conflict(new { error = "Appointment cannot be rescheduled from its current status" }),
            RescheduleAppointmentResultStatus.SlotConflict => Conflict(new { error = "That slot is no longer available" }),
            RescheduleAppointmentResultStatus.SlotInPast => Conflict(new { error = "That time has already passed", code = "slot_past" }),
            RescheduleAppointmentResultStatus.Success => Ok(MapToDto(result.Appointment!)),
            _ => Problem()
        };
    }

    private static AppointmentDto MapToDto(Models.Appointment a) => new(
        a.Id, a.RefNumber, a.DoctorId, a.PatientId, a.ClinicId, a.SlotStart, a.SlotEnd,
        a.Motif, a.Status, a.Notes, null, null, null, null, a.CreatedAt,
        a.AwaitingInsuranceReview ? a.InsuranceReviewDueAt : null,
        a.BookedByUserId == a.PatientId || a.BookedByUserId == Guid.Empty ? null : a.BookedByUserId);
}
