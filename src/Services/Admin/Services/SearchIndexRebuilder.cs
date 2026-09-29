namespace LandaDoc.Admin.Services;

// Once the app (and so the MassTransit bus) has started, re-sends every approved
// doctor so the Search service's list is rebuilt after a Redis wipe or lost messages.
public class SearchIndexRebuilder(
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    ILogger<SearchIndexRebuilder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource();
        using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
        using (stoppingToken.Register(() => started.TrySetCanceled()))
        {
            try { await started.Task; }
            catch (TaskCanceledException) { return; }
        }

        try
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider
                .GetRequiredService<IDoctorProfileService>()
                .RepublishApprovedAsync(stoppingToken);
            logger.LogInformation("Re-sent {Count} approved doctors to Search", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Couldn't re-send approved doctors to Search");
        }
    }
}
