using System.Security.Claims;

namespace LandaDoc.Shared.Models;

// Which ways to pay are offered, by the country the payer lives in (their ISO code, carried in
// the access token's ClaimTypes.Country claim). In the DRC it's mobile money and insurance only; every
// other country (or no country on file) also gets card.
public static class PaymentMethods
{
    public const string CountryClaim = ClaimTypes.Country;

    private static readonly HashSet<string> NoCardCountries = new(StringComparer.OrdinalIgnoreCase) { "CD" };

    public static bool CardAllowed(string? country) =>
        string.IsNullOrWhiteSpace(country) || !NoCardCountries.Contains(country.Trim());

    public static bool IsAllowed(PaymentProvider provider, string? country) =>
        provider != PaymentProvider.Stripe || CardAllowed(country);
}
