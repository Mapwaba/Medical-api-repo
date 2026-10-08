using System.Text;
using Microsoft.Extensions.Configuration;

namespace LandaDoc.ServiceDefaults;

// The key every service signs (Identity) or checks (all of them) access tokens with. It is never
// committed: locally it comes from dotnet user-secrets (tools/set-dev-secrets.ps1 sets the same
// value in every service), elsewhere from the Jwt__Secret environment variable. A service
// without it stops at startup with a message saying how to fix it, rather than starting with a
// guessable key or failing on the first request.
public static class JwtSecret
{
    // HMAC-SHA256 needs at least 256 bits; the token library refuses shorter keys anyway
    private const int MinimumBytes = 32;

    public static string GetJwtSecret(this IConfiguration cfg)
    {
        var secret = cfg["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(
                "Jwt:Secret is not set. Locally, run tools/set-dev-secrets.ps1 (or `dotnet user-secrets set \"Jwt:Secret\" <key>` " +
                "in this service's folder); on a server, set the Jwt__Secret environment variable. Every service needs the same value.");
        if (Encoding.UTF8.GetByteCount(secret) < MinimumBytes)
            throw new InvalidOperationException(
                $"Jwt:Secret is too short: it needs at least {MinimumBytes} bytes. Generate one with `openssl rand -base64 32`.");
        return secret;
    }
}
