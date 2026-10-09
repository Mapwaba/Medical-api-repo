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

    private static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
