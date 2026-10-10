using LandaDoc.Availability.Data;
using LandaDoc.Availability.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using StackExchange.Redis;

namespace LandaDoc.Availability.Tests;

// Slots that have already started on the doctor's clock aren't offered
public class SlotTimingTests
{
    // 9 September 2026, 14:32 UTC: 15:32 in Kinshasa, 16:32 in Lubumbashi
    private static readonly DateTime UtcAfternoon = new(2026, 9, 9, 14, 32, 0, DateTimeKind.Utc);
    private static readonly DateOnly September9 = new(2026, 9, 9);

    // A 09:00-17:00 day in 30-minute slots
    private static readonly List<string> WorkingDay = Enumerable.Range(0, 16)
        .Select(i => new TimeOnly(9, 0).AddMinutes(30 * i).ToString("HH:mm")).ToList();

    [Theory]
    [InlineData("Africa/Kinshasa", 15, 32)]
    [InlineData("Africa/Lubumbashi", 16, 32)]
    [InlineData("Not/AZone", 15, 32)]     // unknown: read as Kinshasa
    [InlineData(null, 15, 32)]
    public void The_clock_reads_the_doctors_zone(string? zone, int hour, int minute)
    {
        var now = LocalClock.Now(zone, UtcAfternoon);
        Assert.Equal(new DateTime(2026, 9, 9, hour, minute, 0), now);
    }

    // What a viewer elsewhere sees next to each time (the Patient app's "11:00 your time")
    [Theory]
    [InlineData("Africa/Kinshasa", "16:00", "America/Toronto", "2026-10-09 11:00")]     // UTC+1 vs EDT (UTC-4)
    [InlineData("Africa/Lubumbashi", "16:00", "America/Toronto", "2026-10-09 10:00")]
    [InlineData("Africa/Kinshasa", "16:00", "Africa/Kinshasa", "2026-10-09 16:00")]     // same clock: nothing to add
    [InlineData("Africa/Kinshasa", "16:00", "Africa/Lagos", "2026-10-09 16:00")]        // another UTC+1 zone reads the same
    [InlineData("Africa/Kinshasa", "23:00", "Asia/Tokyo", "2026-10-10 07:00")]          // the viewer's next day
    public void A_slot_is_shown_on_the_viewers_clock(string doctorZone, string slot, string viewerZone, string expected)
    {
        var viewer = TimeZoneInfo.FindSystemTimeZoneById(viewerZone);

        var onViewersClock = LocalClock.OnClock(new DateOnly(2026, 10, 9), TimeOnly.Parse(slot), doctorZone, viewer);

        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), onViewersClock);
    }

    [Fact]
    public void The_doctors_zone_is_named_by_its_city()
    {
        Assert.Equal("Lubumbashi", LocalClock.CityOf("Africa/Lubumbashi"));
        Assert.Equal("Kinshasa", LocalClock.CityOf(null));
        Assert.Equal(new DateTime(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc), LocalClock.ToUtc(new DateTime(2026, 10, 9, 16, 0, 0), "Africa/Kinshasa"));
    }

    [Fact]
    public void At_3_32_PM_in_Kinshasa_only_the_slots_still_ahead_are_offered()
    {
        var offered = AvailabilityService.StillBookable(WorkingDay, September9, LocalClock.Now("Africa/Kinshasa", UtcAfternoon));

        // 15:30 started two minutes ago; 09:00 to 15:00 are long gone
        Assert.Equal(["16:00", "16:30"], offered);
    }

    [Fact]
    public void The_same_moment_in_Lubumbashi_is_an_hour_later()
    {
        var offered = AvailabilityService.StillBookable(WorkingDay, September9, LocalClock.Now("Africa/Lubumbashi", UtcAfternoon));

        Assert.Empty(offered);   // 16:32 there: the last slot, 16:30, has started too
    }

    // A week in one request: each day as it would be asked for alone, past days empty
    [Fact]
    public async Task A_range_returns_each_day_with_the_doctors_zone()
    {
        var service = Service(out _);
        var doctorId = Guid.NewGuid();
        await service.SetScheduleAsync(doctorId,
            [new ScheduleDayDto(DayOfWeekEnum.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), 30)], "Africa/Lubumbashi");
        var yesterday = DateOnly.FromDateTime(LocalClock.Now("Africa/Lubumbashi")).AddDays(-1);

        var range = await service.GetSlotRangeAsync(doctorId, yesterday, 8);

        Assert.Equal("Africa/Lubumbashi", range.TimeZone);
        Assert.Equal(8, range.Days.Count);
        Assert.Equal(yesterday, range.Days[0].Date);
        Assert.Empty(range.Days[0].Slots);   // the past
        var nextMonday = range.Days.Skip(1).Single(d => d.Date.DayOfWeek == DayOfWeek.Monday && d.Date > range.Days[1].Date);
        Assert.Equal(["09:00", "09:30"], nextMonday.Slots);
        Assert.All(range.Days.Where(d => d.Date.DayOfWeek != DayOfWeek.Monday), d => Assert.Empty(d.Slots));
    }

    // A doctor takes slots out and gives them back; patients see the change at once
    [Fact]
    public async Task Blocked_slots_leave_the_free_times_and_come_back_when_unblocked()
    {
        var service = Service(out _);
        var doctorId = Guid.NewGuid();
        await service.SetScheduleAsync(doctorId,
            Enum.GetValues<DayOfWeekEnum>().Select(d => new ScheduleDayDto(d, new TimeOnly(9, 0), new TimeOnly(11, 0), 30)).ToList());
        var tomorrow = DateOnly.FromDateTime(LocalClock.Now(null)).AddDays(1);
        DateTime At(int h, int m) => tomorrow.ToDateTime(new TimeOnly(h, m), DateTimeKind.Utc);

        var blocked = await service.BlockAsync(doctorId, [At(9, 30), At(10, 0), At(9, 30)], "Staff meeting");
        var again = await service.BlockAsync(doctorId, [At(9, 30)], null);

        Assert.Equal(2, blocked);   // the repeated 09:30 counts once
        Assert.Equal(0, again);
        Assert.Equal(["09:00", "10:30"], await service.GetSlotsAsync(doctorId, tomorrow));
        var list = await service.GetBlockedAsync(doctorId, tomorrow, 1);
        Assert.Equal([At(9, 30), At(10, 0)], list.Select(b => b.SlotStart));
        Assert.All(list, b => Assert.Equal("Staff meeting", b.Reason));

        Assert.Equal(1, await service.UnblockAsync(doctorId, [At(10, 0)]));
        Assert.Equal(["09:00", "10:00", "10:30"], await service.GetSlotsAsync(doctorId, tomorrow));
        Assert.Empty(await service.GetBlockedAsync(doctorId, tomorrow.AddDays(1), 7));   // other days untouched
    }

    // A day closing just before midnight used to make slot generation loop forever
    [Fact]
    public async Task A_day_closing_just_before_midnight_ends()
    {
        var service = Service(out _);
        var doctorId = Guid.NewGuid();
        await service.SetScheduleAsync(doctorId,
            Enum.GetValues<DayOfWeekEnum>().Select(d => new ScheduleDayDto(d, new TimeOnly(22, 0), new TimeOnly(23, 59), 30)).ToList());

        var slots = await service.GetSlotsAsync(doctorId, DateOnly.FromDateTime(LocalClock.Now(null)).AddDays(1)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["22:00", "22:30", "23:00"], slots);
    }

    [Fact]
    public void A_slot_starting_this_minute_is_no_longer_offered()
    {
        var offered = AvailabilityService.StillBookable(["10:00", "10:30"], September9, new DateTime(2026, 9, 9, 10, 0, 0));

        Assert.Equal(["10:30"], offered);
    }

    [Fact]
    public void Past_days_offer_nothing_and_future_days_everything()
    {
        var now = LocalClock.Now("Africa/Kinshasa", UtcAfternoon);

        Assert.Empty(AvailabilityService.StillBookable(WorkingDay, September9.AddDays(-1), now));
        Assert.Equal(WorkingDay, AvailabilityService.StillBookable(WorkingDay, September9.AddDays(1), now));
    }

    [Fact]
    public void Just_after_midnight_the_whole_new_day_is_offered()
    {
        // 23:10 UTC on the 9th is already 00:10 on the 10th in Kinshasa
        var now = LocalClock.Now("Africa/Kinshasa", new DateTime(2026, 9, 9, 23, 10, 0, DateTimeKind.Utc));

        Assert.Empty(AvailabilityService.StillBookable(WorkingDay, September9, now));
        Assert.Equal(WorkingDay, AvailabilityService.StillBookable(WorkingDay, September9.AddDays(1), now));
    }

    // The service over an in-memory database, with a Redis cache that always misses
    private static AvailabilityService Service(out AvailabilityDbContext db)
    {
        db = new AvailabilityDbContext(new DbContextOptionsBuilder<AvailabilityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var redis = Substitute.For<IConnectionMultiplexer>();
        var cache = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(cache);
        cache.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
        return new AvailabilityService(db, redis);
    }

    // The whole service, with the real clock: yesterday is empty, tomorrow is full
    [Fact]
    public async Task The_service_applies_the_doctors_zone_saved_with_the_schedule()
    {
        var service = Service(out var db);

        var doctorId = Guid.NewGuid();
        var allDays = Enum.GetValues<DayOfWeekEnum>().Select(d => new ScheduleDayDto(d, new TimeOnly(0, 0), new TimeOnly(23, 59), 30)).ToList();
        await service.SetScheduleAsync(doctorId, allDays, "Africa/Lubumbashi");
        Assert.All(db.Schedules, s => Assert.Equal("Africa/Lubumbashi", s.TimeZone));

        var today = DateOnly.FromDateTime(LocalClock.Now("Africa/Lubumbashi"));
        Assert.Empty(await service.GetSlotsAsync(doctorId, today.AddDays(-1)));
        Assert.Equal(47, (await service.GetSlotsAsync(doctorId, today.AddDays(1))).Count);   // 00:00 to 23:00
        Assert.DoesNotContain("00:00", await service.GetSlotsAsync(doctorId, today));      // started hours ago... or just now

        // Saving the hours again without a zone keeps Lubumbashi
        await service.SetScheduleAsync(doctorId, allDays);
        Assert.All(db.Schedules, s => Assert.Equal("Africa/Lubumbashi", s.TimeZone));
    }
}
