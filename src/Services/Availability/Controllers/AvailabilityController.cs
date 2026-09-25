using System.Security.Claims;
using LandaDoc.Availability.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Availability.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvailabilityController(IAvailabilityService availability) : ControllerBase
{
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots([FromQuery] Guid doctorId, [FromQuery] DateOnly date) =>
        Ok(new SlotsResponse(await availability.GetSlotsAsync(doctorId, date)));

    [HttpGet("schedule/mine")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> GetMySchedule()
    {
        var doctorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await availability.GetScheduleAsync(doctorId));
    }

    [HttpPut("schedule/mine")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> SetMySchedule([FromBody] UpdateScheduleRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var doctorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await availability.SetScheduleAsync(doctorId, req.Days);
        return Ok(await availability.GetScheduleAsync(doctorId));
    }
}
