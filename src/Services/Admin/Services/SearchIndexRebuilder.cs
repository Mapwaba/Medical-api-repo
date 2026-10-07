using LandaDoc.Shared.Models;

namespace LandaDoc.Admin.Services;

// Once the app (and so the MassTransit bus) has started, approves any pending
// doctors (only when Doctors:RequireApproval is false) and re-sends every approved
// doctor so the Search service's list is rebuilt after a Redis wipe or lost messages.
public class SearchIndexRebuilder(
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    IConfiguration config,
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
            var doctors = scope.ServiceProvider.GetRequiredService<IDoctorProfileService>();

            // Pending doctors wait for an admin by default. Only when review is turned
            // off (Doctors__RequireApproval=false) are leftover Pending profiles approved here.
            if (!config.GetValue("Doctors:RequireApproval", true))
            {
                var pending = await doctors.GetAllAsync(DoctorApprovalStatus.Pending);
                foreach (var profile in pending)
                    await doctors.ApproveAsync(profile.Id);
                if (pending.Count > 0)
                    logger.LogInformation("Approved {Count} pending doctors", pending.Count);
            }

            var count = await doctors.RepublishApprovedAsync(stoppingToken);
            logger.LogInformation("Re-sent {Count} approved doctors to Search", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Couldn't re-send approved doctors to Search");
        }
    }
}
