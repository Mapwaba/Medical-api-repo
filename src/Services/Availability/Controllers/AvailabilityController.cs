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
        Ok(await availability.GetSlotsWithZoneAsync(doctorId, date));

    // Up to two months of days in one request (default a week), e.g. a calendar's free times
    [HttpGet("slots/range")]
    public async Task<IActionResult> GetSlotRange([FromQuery] Guid doctorId, [FromQuery] DateOnly from, [FromQuery] int days = 7)
    {
        if (days is < 1 or > 62) return BadRequest(new { error = "days must be between 1 and 62" });
        return Ok(await availability.GetSlotRangeAsync(doctorId, from, days));
    }

    // The calling doctor's blocked slots over up to two months (default a week)
    [HttpGet("blocked/mine")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> GetMyBlocked([FromQuery] DateOnly from, [FromQuery] int days = 7)
    {
        if (days is < 1 or > 62) return BadRequest(new { error = "days must be between 1 and 62" });
        return Ok(await availability.GetBlockedAsync(DoctorId(), from, days));
    }

    // Take slots out of the schedule (time off, admin work); a booked slot stays booked
    [HttpPost("blocked/mine")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> BlockMine([FromBody] BlockSlotsRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(new { blocked = await availability.BlockAsync(DoctorId(), req.SlotStarts, req.Reason) });
    }

    // Give blocked slots back to patients
    [HttpPost("blocked/mine/unblock")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> UnblockMine([FromBody] BlockSlotsRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(new { unblocked = await availability.UnblockAsync(DoctorId(), req.SlotStarts) });
    }

    private Guid DoctorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
        await availability.SetScheduleAsync(doctorId, req.Days, req.TimeZone);
        return Ok(await availability.GetScheduleAsync(doctorId));
    }
}
