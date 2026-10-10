using LandaDoc.Shared.DTOs;

namespace LandaDoc.Availability.Services;

public interface IAvailabilityService
{
    Task<List<string>> GetSlotsAsync(Guid doctorId, DateOnly date);
    Task<SlotsResponse> GetSlotsWithZoneAsync(Guid doctorId, DateOnly date);
    Task<SlotRangeResponse> GetSlotRangeAsync(Guid doctorId, DateOnly from, int days);
    Task InvalidateCacheAsync(Guid doctorId, DateOnly date);
    Task<List<ScheduleDayDto>> GetScheduleAsync(Guid doctorId);
    Task SetScheduleAsync(Guid doctorId, List<ScheduleDayDto> days, string? timeZone = null);
}
