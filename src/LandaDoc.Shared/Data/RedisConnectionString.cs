namespace LandaDoc.Shared.Data;

// Managed Redis hosts (Render Key Value, Upstash...) hand out a URL of the form
// redis[s]://[user:pass@]host:port, but StackExchange.Redis only understands
// "host:port,password=...,ssl=true". Accept both.
public static class RedisConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
              value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase)))
            return value;

        var uri = new Uri(value);
        var port = uri.IsDefaultPort || uri.Port <= 0 ? 6379 : uri.Port;
        var result = $"{uri.Host}:{port}";

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userInfo = uri.UserInfo.Split(':', 2);
            if (userInfo.Length == 2)
            {
                if (userInfo[0].Length > 0)
                    result += $",user={Uri.UnescapeDataString(userInfo[0])}";
                result += $",password={Uri.UnescapeDataString(userInfo[1])}";
            }
            else
                result += $",password={Uri.UnescapeDataString(userInfo[0])}";
        }

        if (uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
            result += ",ssl=true";
        return result;
    }
}
