using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.DTOs;

public record ScheduleDayDto(
    DayOfWeekEnum Day,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    [Range(5, 240)] int SlotMinutes
);

// TimeZone: the IANA zone the hours are in ("Africa/Lubumbashi"), sent by the Doctor app from the
// doctor's browser; null keeps the one saved before (Kinshasa by default). See LocalClock.
public record UpdateScheduleRequest([Required] List<ScheduleDayDto> Days, [StringLength(64)] string? TimeZone = null);

// Slots are "HH:mm" on the doctor's clock; TimeZone is that clock's IANA zone ("Africa/Kinshasa"),
// null when the doctor doesn't work that day. The Patient app uses it to show the viewer's time too.
public record SlotsResponse(List<string> Slots, string? TimeZone = null);

// Several days' free slots in one answer, for a calendar (the mobile app asks for a week or a
// month at once instead of one request per day). Same rules as SlotsResponse for each day.
public record DaySlotsDto(DateOnly Date, List<string> Slots);
public record SlotRangeResponse(List<DaySlotsDto> Days, string? TimeZone = null);

// A slot the doctor has taken out (time off, admin work) on top of their weekly schedule.
// SlotStart is wall-clock time on the doctor's clock, like appointment times.
public record BlockedSlotDto(DateTime SlotStart, string? Reason);

// Block or unblock several slots at once (a whole day = all of that day's times)
public record BlockSlotsRequest(
    [Required, MinLength(1), MaxLength(200)] List<DateTime> SlotStarts,
    [StringLength(200)] string? Reason = null);
