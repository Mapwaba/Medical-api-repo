using LandaDoc.Payment.Data;
using LandaDoc.Payment.Models;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LandaDoc.Payment.Tests;

// A callback that never arrived: the reconciliation job asks FreshPay and records the outcome
public class MobileMoneyReconciliationTests
{
    private readonly IMokoAfrikaClient _moko = Substitute.For<IMokoAfrikaClient>();
    private readonly IPublishEndpoint _bus = Substitute.For<IPublishEndpoint>();
    private readonly ServiceProvider _services;

    public MobileMoneyReconciliationTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddDbContext<PaymentDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddSingleton(_moko)
            .AddSingleton(_bus)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddScoped<MobileMoneySettlement>()
            .BuildServiceProvider();
    }

    private MobileMoneyReconciliationService Job() => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MokoAfrika:ReconcileAfterSeconds"] = "60",
            ["MokoAfrika:ReconcileForHours"] = "24",
        }).Build(),
        NullLogger<MobileMoneyReconciliationService>.Instance);

    private MobileMoneyAttempt Prompt(TimeSpan age, string? providerId = null)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = new Models.Payment
        {
            AppointmentId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            GrossAmount = 50m, PlatformFee = 5m, NetAmount = 45m,
        };
        var attempt = new MobileMoneyAttempt
        {
            PaymentId = payment.Id,
            Reference = $"{payment.Id:N}_{Guid.NewGuid():N}",
            ProviderTransactionId = providerId,
            CreatedAt = DateTime.UtcNow - age,
        };
        db.Payments.Add(payment);
        db.MobileMoneyAttempts.Add(attempt);
        db.SaveChanges();
        return attempt;
    }

    private PaymentDbContext Db() => _services.CreateScope().ServiceProvider.GetRequiredService<PaymentDbContext>();

    [Fact]
    public async Task A_payment_FreshPay_completed_is_recorded_without_a_callback()
    {
        var attempt = Prompt(TimeSpan.FromMinutes(5));
        _moko.VerifyAsync(attempt.Reference, Arg.Any<CancellationToken>())
            .Returns(new MokoVerifyResult(true, "Success", "PD777", "Transaction successful"));

        var recorded = await Job().SweepAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(1, recorded);
        var db = Db();
        Assert.Equal(PaymentStatus.Completed, (await db.Payments.SingleAsync(p => p.ProviderRef == "PD777")).Status);
        Assert.NotNull((await db.MobileMoneyAttempts.SingleAsync()).ResolvedAt);
        await _bus.Received(1).Publish(Arg.Any<PaymentCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_payment_still_in_progress_is_asked_about_again_later()
    {
        var attempt = Prompt(TimeSpan.FromMinutes(5));
        _moko.VerifyAsync(attempt.Reference, Arg.Any<CancellationToken>())
            .Returns(new MokoVerifyResult(true, "Pending", "PD777", null));

        var recorded = await Job().SweepAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(0, recorded);
        var saved = await Db().MobileMoneyAttempts.SingleAsync();
        Assert.Null(saved.ResolvedAt);
        Assert.NotNull(saved.LastCheckedAt);
    }

    [Fact]
    public async Task FreshPays_own_id_is_tried_when_our_reference_is_unknown_to_it()
    {
        var attempt = Prompt(TimeSpan.FromMinutes(5), providerId: "PD777");
        _moko.VerifyAsync(attempt.Reference, Arg.Any<CancellationToken>()).Returns(new MokoVerifyResult(false, null, null, "not recognized"));
        _moko.VerifyAsync("PD777", Arg.Any<CancellationToken>()).Returns(new MokoVerifyResult(true, "Failed", "PD777", "Annulé"));

        var recorded = await Job().SweepAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(1, recorded);
        Assert.Equal(PaymentStatus.Failed, (await Db().Payments.SingleAsync(p => p.ProviderRef == "PD777")).Status);
    }

    [Fact]
    public async Task Prompts_too_new_or_too_old_are_left_alone()
    {
        Prompt(TimeSpan.FromSeconds(10));   // the patient may still be approving it
        Prompt(TimeSpan.FromDays(2));       // long expired

        await Job().SweepAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        await _moko.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
    }

    [Fact]
    public async Task A_payment_a_callback_already_recorded_is_not_recorded_again()
    {
        var attempt = Prompt(TimeSpan.FromMinutes(5));
        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MobileMoneySettlement>()
                .ApplyAsync(attempt.Reference, "PD777", "Success", null, "callback");

        await Job().SweepAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        await _moko.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);   // resolved: not asked about
        Assert.Equal(1, await Db().Payments.CountAsync(p => p.ProviderRef == "PD777"));
    }
}
