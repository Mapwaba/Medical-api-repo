using LandaDoc.Shared.DTOs;

namespace LandaDoc.Availability.Services;

public interface IAvailabilityService
{
    Task<List<string>> GetSlotsAsync(Guid doctorId, DateOnly date);
    Task<SlotsResponse> GetSlotsWithZoneAsync(Guid doctorId, DateOnly date);
    Task<SlotRangeResponse> GetSlotRangeAsync(Guid doctorId, DateOnly from, int days);
    Task<List<BlockedSlotDto>> GetBlockedAsync(Guid doctorId, DateOnly from, int days);
    // How many slots were newly blocked / unblocked (repeats are ignored)
    Task<int> BlockAsync(Guid doctorId, IEnumerable<DateTime> slotStarts, string? reason);
    Task<int> UnblockAsync(Guid doctorId, IEnumerable<DateTime> slotStarts);
    Task InvalidateCacheAsync(Guid doctorId, DateOnly date);
    Task<List<ScheduleDayDto>> GetScheduleAsync(Guid doctorId);
    Task SetScheduleAsync(Guid doctorId, List<ScheduleDayDto> days, string? timeZone = null);
}
