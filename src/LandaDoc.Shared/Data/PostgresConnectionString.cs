namespace LandaDoc.Shared.Data;

// Managed Postgres hosts (Render, Heroku, Neon...) hand out a URL of the form
// postgresql://user:pass@host[:port]/db, but Npgsql only understands the
// "Host=...;Username=..." keyword format. Accept both so the same service can
// run against local docker and a hosted database with no code change.
public static class PostgresConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
              value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        var result = $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password}";

        // Carry over query options such as ?sslmode=require.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
                result += $";{Uri.UnescapeDataString(kv[0])}={Uri.UnescapeDataString(kv[1])}";
        }
        return result;
    }
}
