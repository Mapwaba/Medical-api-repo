using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.DTOs;

public record ScheduleDayDto(
    DayOfWeekEnum Day,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    [Range(5, 240)] int SlotMinutes
);

public record UpdateScheduleRequest([Required] List<ScheduleDayDto> Days);

public record SlotsResponse(List<string> Slots);
