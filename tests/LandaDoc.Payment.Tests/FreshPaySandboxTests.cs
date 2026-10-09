using LandaDoc.Payment.Data;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LandaDoc.Payment.Tests;

// Live checks against Moko Afrika's sandbox (sandbox.gofreshpay.com), using its test numbers:
// <prefix>0000001 succeeds, …2 fails (insufficient balance), …3 is pending then succeeds (~15 s),
// …4 is pending then cancelled, …5 never gets a callback. They run only when sandbox credentials
// are in the environment, so the usual test run stays offline:
//   MOKO_SANDBOX_MERCHANT_ID, MOKO_SANDBOX_MERCHANT_SECRET
//   (optional MOKO_SANDBOX_URL, default https://sandbox.gofreshpay.com/api/v1/gateway)
// No callback can reach a developer machine, so outcomes are read with verify, the same way the
// reconciliation job learns about a lost callback.
public class FreshPaySandboxTests
{
    private static readonly string? MerchantId = Environment.GetEnvironmentVariable("MOKO_SANDBOX_MERCHANT_ID");
    private static readonly string? MerchantSecret = Environment.GetEnvironmentVariable("MOKO_SANDBOX_MERCHANT_SECRET");
    private static readonly string Url = Environment.GetEnvironmentVariable("MOKO_SANDBOX_URL") ?? "https://sandbox.gofreshpay.com/api/v1/gateway";

    private static MokoAfrikaClient Client()
    {
        Skip.If(string.IsNullOrWhiteSpace(MerchantId) || string.IsNullOrWhiteSpace(MerchantSecret),
            "Set MOKO_SANDBOX_MERCHANT_ID and MOKO_SANDBOX_MERCHANT_SECRET to run the sandbox tests");
        return new MokoAfrikaClient(new HttpClient { BaseAddress = new Uri(Url), Timeout = TimeSpan.FromSeconds(60) },
            Options.Create(new MokoAfrikaOptions { MerchantId = MerchantId!, MerchantSecret = MerchantSecret! }));
    }

    // Starts a 1 USD debit on a test number and reads its status with verify until it's final
    // (or the time is up); returns the last status seen and every status on the way
    private static async Task<(string? Final, List<string?> Seen)> PayAsync(MobileMoneyOperator op, string phone, TimeSpan wait)
    {
        var client = Client();
        var reference = $"{Guid.NewGuid():N}_{Guid.NewGuid():N}";   // the shape the app sends
        var started = await client.InitiateDebitAsync(reference, 1m, phone, op, "Test", "LandaDoc", "test@landadoc.test",
            "https://example.invalid/api/payments/webhook/moko");
        Assert.True(started.Success, $"FreshPay refused the request: {started.Comment}");
        Assert.False(string.IsNullOrEmpty(started.TransactionId));

        var seen = new List<string?>();
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            var status = await client.VerifyAsync(reference);
            Assert.True(status.Found, $"verify didn't find {reference}: {status.Description}");
            seen.Add(status.TransStatus);
            if (status.IsFinal || DateTime.UtcNow > deadline) return (status.TransStatus, seen);
        }
    }

    public static TheoryData<MobileMoneyOperator, string> Operators => new()
    {
        { MobileMoneyOperator.Mpesa, "081000000" },
        { MobileMoneyOperator.Orange, "084000000" },
        { MobileMoneyOperator.Airtel, "097000000" },
    };

    [SkippableTheory]
    [MemberData(nameof(Operators))]
    public async Task Succeeds(MobileMoneyOperator op, string prefix)
    {
        var (final, _) = await PayAsync(op, prefix + "1", TimeSpan.FromSeconds(30));
        Assert.True(MokoStatus.IsSuccess(final), $"ended as {final}");
    }

    [SkippableTheory]
    [MemberData(nameof(Operators))]
    public async Task Fails_for_insufficient_balance(MobileMoneyOperator op, string prefix)
    {
        var (final, _) = await PayAsync(op, prefix + "2", TimeSpan.FromSeconds(30));
        Assert.True(MokoStatus.IsFailure(final), $"ended as {final}");
    }

    [SkippableTheory]
    [MemberData(nameof(Operators))]
    public async Task Is_pending_then_succeeds(MobileMoneyOperator op, string prefix)
    {
        var (final, seen) = await PayAsync(op, prefix + "3", TimeSpan.FromSeconds(60));
        Assert.Contains(seen, s => !MokoStatus.IsFinal(s));   // pending first
        Assert.True(MokoStatus.IsSuccess(final), $"ended as {final} after {string.Join(", ", seen)}");
    }

    [SkippableTheory]
    [MemberData(nameof(Operators))]
    public async Task Is_pending_then_cancelled(MobileMoneyOperator op, string prefix)
    {
        var (final, seen) = await PayAsync(op, prefix + "4", TimeSpan.FromSeconds(60));
        Assert.Contains(seen, s => !MokoStatus.IsFinal(s));
        Assert.True(MokoStatus.IsFailure(final), $"ended as {final} after {string.Join(", ", seen)}");
    }

    // End to end without a callback (none can reach this machine): the app records the prompt,
    // the reconciliation job asks the sandbox, and the booking is confirmed in the ledger
    [SkippableTheory]
    [InlineData("0810000001", PaymentStatus.Completed)]   // succeeds within seconds
    [InlineData("0810000003", PaymentStatus.Completed)]   // pending, then succeeds after ~15 s
    [InlineData("0810000002", PaymentStatus.Failed)]      // insufficient balance
    public async Task A_lost_callback_is_recovered_by_reconciliation(string phone, PaymentStatus expected)
    {
        var client = Client();
        var dbName = Guid.NewGuid().ToString();
        var bus = Substitute.For<IPublishEndpoint>();
        using var services = new ServiceCollection()
            .AddDbContext<PaymentDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddSingleton<IMokoAfrikaClient>(client)
            .AddSingleton(bus)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped<MobileMoneySettlement>()
            .BuildServiceProvider();

        // What PaymentsController.Initiate does: the Pending row, the prompt, the attempt
        var payment = new Models.Payment { AppointmentId = Guid.NewGuid(), GrossAmount = 1m, NetAmount = 0.9m, PlatformFee = 0.1m };
        var reference = $"{payment.Id:N}_{Guid.NewGuid():N}";
        var started = await client.InitiateDebitAsync(reference, 1m, phone, MobileMoneyOperator.Mpesa, "Test", "LandaDoc", "test@landadoc.test",
            "https://example.invalid/api/payments/webhook/moko");
        Assert.True(started.Success, started.Comment);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.Payments.Add(payment);
            db.MobileMoneyAttempts.Add(new Models.MobileMoneyAttempt
            {
                PaymentId = payment.Id, Reference = reference, ProviderTransactionId = started.TransactionId,
                Operator = MobileMoneyOperator.Mpesa, Amount = 1m, CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            });
            await db.SaveChangesAsync();
        }

        var job = new MobileMoneyReconciliationService(
            services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<MobileMoneyReconciliationService>.Instance);

        // Sweep every 5 s for up to a minute, as the job does every minute
        var recorded = 0;
        for (var i = 0; i < 12 && recorded == 0; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            recorded = await job.SweepAsync(TimeSpan.Zero, CancellationToken.None);
        }

        Assert.Equal(1, recorded);
        using var check = services.CreateScope();
        var ledger = check.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var row = Assert.Single(ledger.Payments.Where(p => p.ProviderRef == started.TransactionId));
        Assert.Equal(expected, row.Status);
    }

    // The case the reconciliation job exists for: no callback, and verify keeps saying it isn't
    // final, so the app must neither confirm nor fail it on its own
    [SkippableTheory]
    [MemberData(nameof(Operators))]
    public async Task Times_out_without_a_result(MobileMoneyOperator op, string prefix)
    {
        var (final, seen) = await PayAsync(op, prefix + "5", TimeSpan.FromSeconds(30));
        Assert.False(MokoStatus.IsFinal(final), $"ended as {final} after {string.Join(", ", seen)}");
    }
}
