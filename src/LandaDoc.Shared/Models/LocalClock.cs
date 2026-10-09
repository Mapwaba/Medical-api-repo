namespace LandaDoc.Shared.Models;

// Appointment slots are wall-clock times on the doctor's clock ("09:30" means 9:30 where the
// doctor works), stored without a time zone. To tell which ones have already started, "now" has
// to be read on that same clock. The DRC spans two zones: Africa/Kinshasa (UTC+1, the west) and
// Africa/Lubumbashi (UTC+2, Lubumbashi, Goma, Bukavu, Kisangani, Mbuji-Mayi...).
public static class LocalClock
{
    public const string DefaultTimeZone = "Africa/Kinshasa";

    // Used when the server has no time zone database (e.g. a slim container image)
    private static readonly Dictionary<string, TimeSpan> KnownOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Africa/Kinshasa"] = TimeSpan.FromHours(1),
        ["Africa/Lubumbashi"] = TimeSpan.FromHours(2),
    };

    public static bool IsKnown(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && (KnownOffsets.ContainsKey(timeZoneId) || TryFind(timeZoneId) is not null);

    // The wall-clock time now in that zone (DateTimeKind.Unspecified); an unknown zone reads as Kinshasa
    public static DateTime Now(string? timeZoneId, DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        var id = IsKnown(timeZoneId) ? timeZoneId! : DefaultTimeZone;
        var local = TryFind(id) is { } zone
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            : utc + KnownOffsets[id];
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    // The UTC moment a wall-clock time in that zone falls on; an unknown zone reads as Kinshasa
    public static DateTime ToUtc(DateTime wallClock, string? timeZoneId)
    {
        var id = IsKnown(timeZoneId) ? timeZoneId! : DefaultTimeZone;
        var wall = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (TryFind(id) is not { } zone)
            return DateTime.SpecifyKind(wall - KnownOffsets[id], DateTimeKind.Utc);
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(wall, zone);
        }
        catch (ArgumentException)
        {
            // A time skipped by a daylight-saving change (none in the DRC): use the zone's offset then
            return DateTime.SpecifyKind(wall - zone.GetUtcOffset(wall), DateTimeKind.Utc);
        }
    }

    // What another clock (the viewer's) reads when a slot on the doctor's clock begins
    public static DateTime OnClock(DateOnly date, TimeOnly time, string? doctorZone, TimeZoneInfo viewerZone) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(ToUtc(date.ToDateTime(time), doctorZone), viewerZone), DateTimeKind.Unspecified);

    // "Africa/Lubumbashi" → "Lubumbashi", for "Times are Lubumbashi time"
    public static string CityOf(string? timeZoneId)
    {
        var id = IsKnown(timeZoneId) ? timeZoneId! : DefaultTimeZone;
        return id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ');
    }

    private static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
