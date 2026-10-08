using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace LandaDoc.ServiceDefaults;

// Logging setup shared by every backend service, so all of them log the same way:
//  - always to the console (Render shows a short recent history per service);
//  - to Seq when Seq__Url (or the older SEQ_URL) is set — handy for local development;
//  - to Better Stack when BetterStack__SourceToken is set — one searchable place for all services.
//    BetterStack__Endpoint is the source's ingesting host (shown next to the token in Better Stack).
// Every line carries a "Service" property, so logs from all services can be read side by side.
// Framework chatter (each SQL command, routing details) is turned down to warnings, and each HTTP
// request becomes one short line instead of several, which keeps hosted-log quotas for what matters.
public static class LandaDocLogging
{
    public static WebApplicationBuilder AddLandaDocLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var cfg = builder.Configuration;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information) // "service started"
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console();

        var seqUrl = cfg["Seq:Url"] ?? Environment.GetEnvironmentVariable("SEQ_URL");
        if (!string.IsNullOrWhiteSpace(seqUrl))
            logger.WriteTo.Seq(seqUrl);

        var betterStackToken = cfg["BetterStack:SourceToken"];
        if (!string.IsNullOrWhiteSpace(betterStackToken))
        {
            var endpoint = cfg["BetterStack:Endpoint"];
            logger.WriteTo.BetterStack(
                sourceToken: betterStackToken,
                betterStackEndpoint: string.IsNullOrWhiteSpace(endpoint) ? "https://in.logs.betterstack.com" : WithScheme(endpoint));
        }

        Log.Logger = logger.CreateLogger();
        builder.Host.UseSerilog();
        return builder;
    }

    // One line per HTTP request: method, path, status code and duration (plus the caller's user id
    // when signed in). Call right after builder.Build(), before the other middleware.
    public static WebApplication UseLandaDocRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(o =>
        {
            o.EnrichDiagnosticContext = (diag, http) =>
            {
                var userId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (userId is not null) diag.Set("UserId", userId);
            };
            // Health checks run every few minutes from uptime monitors; keep them out of the way
            o.GetLevel = (http, _, ex) =>
                ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });
        return app;
    }

    // Better Stack shows the ingesting host without "https://"; accept it either way
    private static string WithScheme(string endpoint) =>
        endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : "https://" + endpoint;
}
