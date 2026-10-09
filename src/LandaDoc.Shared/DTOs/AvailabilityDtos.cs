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

public record SlotsResponse(List<string> Slots);
