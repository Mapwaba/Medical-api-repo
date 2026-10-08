namespace LandaDoc.Shared.Locations;

// Countries, provinces and towns for address fields (registration, profiles). Countries are
// stored as their ISO 3166-1 alpha-2 code ("CD"); province and town as text, since for most
// countries they are typed in freely. The data itself is generated: see LocationData.g.cs.
public static partial class Locations
{
    public static IReadOnlyList<Country> Countries => AllCountries;

    public static Country? FindCountry(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null
            : AllCountries.FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsCountry(string? code) => FindCountry(code) is not null;

    // Empty when the country has no list, so the province is typed in
    public static IReadOnlyList<string> ProvincesOf(string? countryCode) =>
        countryCode is not null && ProvinceData.TryGetValue(countryCode.ToUpperInvariant(), out var provinces)
            ? provinces.Select(p => p.Name).ToList()
            : [];

    // Suggestions only: any other town can be typed in
    public static IReadOnlyList<string> TownsOf(string? countryCode, string? province)
    {
        if (countryCode is null || !ProvinceData.TryGetValue(countryCode.ToUpperInvariant(), out var provinces)) return [];
        var match = provinces.FirstOrDefault(p => string.Equals(p.Name, province?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match is not null
            ? match.Towns
            : provinces.SelectMany(p => p.Towns).Distinct().OrderBy(t => t).ToList();
    }
}

public sealed record Country(string Code, string NameEn, string NameFr)
{
    public string Name(string language) => language == "fr" ? NameFr : NameEn;
}

public sealed record Province(string Name, string[] Towns);
