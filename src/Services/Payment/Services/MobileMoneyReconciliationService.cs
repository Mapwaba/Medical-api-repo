using LandaDoc.Payment.Data;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Services;

// FreshPay reports a mobile money outcome by calling back, but a callback can be lost: this service
// asleep or restarting (Render's free plan), a network failure, or a rejected signature. Every
// MokoAfrika:ReconcileIntervalSeconds this asks FreshPay's verify action about prompts still
// unresolved after MokoAfrika:ReconcileAfterSeconds, and records final ones exactly as a callback
// would. After MokoAfrika:ReconcileForHours an attempt is left alone (the booking has long expired).
public class MobileMoneyReconciliationService(
    IServiceScopeFactory scopeFactory,
    IConfiguration cfg,
    ILogger<MobileMoneyReconciliationService> logger) : BackgroundService
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nothing to ask without merchant credentials (e.g. a local run without FreshPay set up)
        if (string.IsNullOrWhiteSpace(cfg["MokoAfrika:MerchantId"]))
        {
            logger.LogInformation("Mobile money reconciliation off: MokoAfrika:MerchantId isn't set");
            return;
        }

        var interval = TimeSpan.FromSeconds(cfg.GetValue("MokoAfrika:ReconcileIntervalSeconds", 60));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(interval, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Mobile money reconciliation sweep failed");
            }
        }
    }

    // One pass: public so tests can run it without waiting for the timer
    public async Task<int> SweepAsync(TimeSpan interval, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var moko = scope.ServiceProvider.GetRequiredService<IMokoAfrikaClient>();
        var settlement = scope.ServiceProvider.GetRequiredService<MobileMoneySettlement>();

        var now = DateTime.UtcNow;
        var oldEnough = now.AddSeconds(-cfg.GetValue("MokoAfrika:ReconcileAfterSeconds", 60));
        var tooOld = now.AddHours(-cfg.GetValue("MokoAfrika:ReconcileForHours", 24));
        var checkedBefore = now - interval;

        var due = await db.MobileMoneyAttempts
            .Where(a => a.ResolvedAt == null && a.CreatedAt <= oldEnough && a.CreatedAt > tooOld
                && (a.LastCheckedAt == null || a.LastCheckedAt <= checkedBefore))
            .OrderBy(a => a.LastCheckedAt ?? a.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        var recorded = 0;
        foreach (var attempt in due)
        {
            attempt.LastCheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var verified = await moko.VerifyAsync(attempt.Reference, ct);
            if (!verified.Found && attempt.ProviderTransactionId is { } providerId)
                verified = await moko.VerifyAsync(providerId, ct);
            if (!verified.Found || !verified.IsFinal) continue;

            var outcome = await settlement.ApplyAsync(attempt.Reference, verified.TransactionId ?? attempt.ProviderTransactionId,
                verified.TransStatus, verified.Description, "reconciliation", ct);
            if (outcome == SettlementOutcome.Recorded)
            {
                recorded++;
                logger.LogInformation("Mobile money attempt {Reference} settled by reconciliation: {Status} (no callback had recorded it)",
                    attempt.Reference, verified.TransStatus);
            }
        }
        return recorded;
    }
}
