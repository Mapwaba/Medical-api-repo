using System.Text.Json;
using LandaDoc.Availability.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LandaDoc.Availability.Services;

public class AvailabilityService(AvailabilityDbContext db, IConnectionMultiplexer redis) : IAvailabilityService
{
    // The day's free slots, without those that have already started on the doctor's clock
    public async Task<List<string>> GetSlotsAsync(Guid doctorId, DateOnly date) =>
        (await GetSlotsWithZoneAsync(doctorId, date)).Slots;

    // The same, with the zone of the doctor's clock the times are on
    public async Task<SlotsResponse> GetSlotsWithZoneAsync(Guid doctorId, DateOnly date)
    {
        var day = await GetDaySlotsAsync(doctorId, date);
        return day is null
            ? new SlotsResponse([])
            : new SlotsResponse(StillBookable(day.Slots, date, LocalClock.Now(day.TimeZone)), day.TimeZone);
    }

    // Each day from `from` on, the same as asking for it alone (each day is cached separately)
    public async Task<SlotRangeResponse> GetSlotRangeAsync(Guid doctorId, DateOnly from, int days)
    {
        var result = new List<DaySlotsDto>(days);
        string? zone = null;
        for (var i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
            var day = await GetSlotsWithZoneAsync(doctorId, date);
            zone ??= day.TimeZone;
            result.Add(new DaySlotsDto(date, day.Slots));
        }
        return new SlotRangeResponse(result, zone);
    }

    // Slots on a past day, or earlier today than now (a slot that's started can't be booked)
    public static List<string> StillBookable(IEnumerable<string> slots, DateOnly date, DateTime localNow)
    {
        var today = DateOnly.FromDateTime(localNow);
        if (date < today) return [];
        if (date > today) return slots.ToList();
        var now = TimeOnly.FromDateTime(localNow);
        return slots.Where(s => TimeOnly.Parse(s) > now).ToList();
    }

    // What's cached: the day's free slots before the clock is applied, and the doctor's zone
    private record DaySlots(string TimeZone, List<string> Slots);

    private async Task<DaySlots?> GetDaySlotsAsync(Guid doctorId, DateOnly date)
    {
        // 1. Check Redis cache first
        var cache = redis.GetDatabase();
        var cacheKey = $"slots:{doctorId}:{date:yyyy-MM-dd}";
        var cached = await cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                if (JsonSerializer.Deserialize<DaySlots>(cached!) is { Slots: not null } hit) return hit;
            }
            catch (JsonException) { } // an entry in the old format (a bare list): recompute
        }

        // 2. Compute from database
        // System.DayOfWeek and DayOfWeekEnum share identical member names.
        var day = Enum.Parse<DayOfWeekEnum>(date.DayOfWeek.ToString());
        var schedule = await db.Schedules
            .Where(s => s.DoctorId == doctorId && s.Day == day)
            .FirstOrDefaultAsync();
        if (schedule is null) return null;

        // Generate all theoretical slots
        // Counted in minutes from midnight: TimeOnly wraps at 24:00, so a day closing after 23:30
        // (e.g. 23:59 with 30-minute slots) used to loop forever
        var slots = new List<string>();
        var open = (int)schedule.OpenTime.ToTimeSpan().TotalMinutes;
        var close = (int)schedule.CloseTime.ToTimeSpan().TotalMinutes;
        for (var start = open; schedule.SlotMinutes > 0 && start + schedule.SlotMinutes <= close; start += schedule.SlotMinutes)
            slots.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(start)).ToString("HH:mm"));

        // Subtract booked and blocked
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var booked = await db.Appointments
            .Where(a => a.DoctorId == doctorId
                && a.SlotStart >= dayStart && a.SlotStart <= dayEnd
                && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.SlotStart.ToString("HH:mm"))
            .ToListAsync();

        var blocked = await db.BlockedSlots
            .Where(b => b.DoctorId == doctorId
                && b.SlotStart >= dayStart && b.SlotStart <= dayEnd)
            .Select(b => b.SlotStart.ToString("HH:mm"))
            .ToListAsync();

        var unavailable = booked.Concat(blocked).ToHashSet();
        var result = new DaySlots(schedule.TimeZone, slots.Where(s => !unavailable.Contains(s)).ToList());

        // 3. Store in Redis for 60 seconds
        await cache.StringSetAsync(cacheKey,
            JsonSerializer.Serialize(result),
            TimeSpan.FromSeconds(60));
        return result;
    }

    // Call this whenever a booking is created or cancelled
    public async Task InvalidateCacheAsync(Guid doctorId, DateOnly date)
    {
        var cache = redis.GetDatabase();
        var cacheKey = $"slots:{doctorId}:{date:yyyy-MM-dd}";
        await cache.KeyDeleteAsync(cacheKey);
    }

    public async Task<List<ScheduleDayDto>> GetScheduleAsync(Guid doctorId) =>
        await db.Schedules
            .Where(s => s.DoctorId == doctorId)
            .Select(s => new ScheduleDayDto(s.Day, s.OpenTime, s.CloseTime, s.SlotMinutes))
            .ToListAsync();

    // Simplest correct approach: full-week replace — delete then insert. Without a (known) time
    // zone, the one the doctor's hours were in before is kept.
    public async Task SetScheduleAsync(Guid doctorId, List<ScheduleDayDto> days, string? timeZone = null)
    {
        var existing = await db.Schedules.Where(s => s.DoctorId == doctorId).ToListAsync();
        var zone = LocalClock.IsKnown(timeZone) ? timeZone!
            : existing.FirstOrDefault()?.TimeZone ?? LocalClock.DefaultTimeZone;
        db.Schedules.RemoveRange(existing);

        foreach (var day in days)
        {
            db.Schedules.Add(new Models.Schedule
            {
                DoctorId = doctorId,
                Day = day.Day,
                OpenTime = day.OpenTime,
                CloseTime = day.CloseTime,
                SlotMinutes = day.SlotMinutes,
                TimeZone = zone,
            });
        }
        await db.SaveChangesAsync();
    }
}
