namespace LandaDoc.Identity.Services;

public static class ClientAddress
{
    // The caller's IP. In production we sit behind a proxy (Render, or Caddy in the Docker setup), so
    // RemoteIpAddress is the proxy's address for everyone; the proxy appends the real client to
    // X-Forwarded-For, and taking the LAST entry means a client can't pose as someone else by sending
    // a made-up header of its own (anything it sends ends up to the left).
    public static string Of(HttpContext ctx)
    {
        var forwardedFor = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Last();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
