using System.Text.Json;
using LandaDoc.Availability.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LandaDoc.Availability.Services;

public class AvailabilityService(AvailabilityDbContext db, IConnectionMultiplexer redis) : IAvailabilityService
{
    public async Task<List<string>> GetSlotsAsync(Guid doctorId, DateOnly date)
    {
        // 1. Check Redis cache first
        var cache = redis.GetDatabase();
        var cacheKey = $"slots:{doctorId}:{date:yyyy-MM-dd}";
        var cached = await cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return JsonSerializer.Deserialize<List<string>>(cached!)!;

        // 2. Compute from database
        // System.DayOfWeek and DayOfWeekEnum share identical member names.
        var day = Enum.Parse<DayOfWeekEnum>(date.DayOfWeek.ToString());
        var schedule = await db.Schedules
            .Where(s => s.DoctorId == doctorId && s.Day == day)
            .FirstOrDefaultAsync();
        if (schedule is null) return [];

        // Generate all theoretical slots
        var slots = new List<string>();
        var cur = schedule.OpenTime;
        while (cur.AddMinutes(schedule.SlotMinutes) <= schedule.CloseTime)
        {
            slots.Add(cur.ToString("HH:mm"));
            cur = cur.AddMinutes(schedule.SlotMinutes);
        }

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
        var result = slots.Where(s => !unavailable.Contains(s)).ToList();

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

    // Simplest correct approach: full-week replace — delete then insert.
    public async Task SetScheduleAsync(Guid doctorId, List<ScheduleDayDto> days)
    {
        var existing = db.Schedules.Where(s => s.DoctorId == doctorId);
        db.Schedules.RemoveRange(existing);

        foreach (var day in days)
        {
            db.Schedules.Add(new Models.Schedule
            {
                DoctorId = doctorId,
                Day = day.Day,
                OpenTime = day.OpenTime,
                CloseTime = day.CloseTime,
                SlotMinutes = day.SlotMinutes
            });
        }
        await db.SaveChangesAsync();
    }
}
