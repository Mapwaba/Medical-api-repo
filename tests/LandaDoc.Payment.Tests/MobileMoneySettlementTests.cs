using System.Text.Json;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Models;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LandaDoc.Payment.Tests;

public class MobileMoneySettlementTests
{
    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IPublishEndpoint _bus = Substitute.For<IPublishEndpoint>();

    private MobileMoneySettlement Settlement => new(_db, _bus, NullLogger<MobileMoneySettlement>.Instance);

    // A booking's Pending ledger row and a prompt sent for it
    private (Models.Payment Payment, MobileMoneyAttempt Attempt) PendingPrompt()
    {
        var payment = new Models.Payment
        {
            AppointmentId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            GrossAmount = 50m, PlatformFee = 5m, NetAmount = 45m, Status = PaymentStatus.Pending,
        };
        var attempt = new MobileMoneyAttempt
        {
            PaymentId = payment.Id,
            Reference = $"{payment.Id:N}_{Guid.NewGuid():N}",
            Operator = MobileMoneyOperator.Airtel,
            Amount = 50m,
        };
        _db.Payments.Add(payment);
        _db.MobileMoneyAttempts.Add(attempt);
        _db.SaveChanges();
        return (payment, attempt);
    }

    [Fact]
    public async Task Success_adds_a_completed_row_and_confirms_the_booking()
    {
        var (payment, attempt) = PendingPrompt();

        var outcome = await Settlement.ApplyAsync(attempt.Reference, "PD123", "Success", "Transaction successful", "test");

        Assert.Equal(SettlementOutcome.Recorded, outcome);
        var row = await _db.Payments.SingleAsync(p => p.ProviderRef == "PD123");
        Assert.Equal(PaymentStatus.Completed, row.Status);
        Assert.Equal(PaymentProvider.MokoAfrika, row.Provider);
        Assert.Equal(payment.AppointmentId, row.AppointmentId);
        Assert.Equal(45m, row.NetAmount);
        Assert.NotNull((await _db.MobileMoneyAttempts.SingleAsync()).ResolvedAt);
        await _bus.Received(1).Publish(Arg.Is<PaymentCompletedEvent>(e => e.AppointmentId == payment.AppointmentId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failure_adds_a_failed_row_with_the_operators_reason()
    {
        var (payment, attempt) = PendingPrompt();

        var outcome = await Settlement.ApplyAsync(attempt.Reference, "PD123", "Failed", "Solde insuffisant", "test");

        Assert.Equal(SettlementOutcome.Recorded, outcome);
        Assert.Equal(PaymentStatus.Failed, (await _db.Payments.SingleAsync(p => p.ProviderRef == "PD123")).Status);
        await _bus.Received(1).Publish(Arg.Is<PaymentFailedEvent>(e => e.AppointmentId == payment.AppointmentId && e.Reason == "Solde insuffisant"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_same_result_twice_is_recorded_once()
    {
        var (_, attempt) = PendingPrompt();

        await Settlement.ApplyAsync(attempt.Reference, "PD123", "Success", null, "callback");
        var second = await Settlement.ApplyAsync(attempt.Reference, "PD123", "Success", null, "reconciliation");

        Assert.Equal(SettlementOutcome.AlreadyRecorded, second);
        Assert.Equal(1, await _db.Payments.CountAsync(p => p.ProviderRef == "PD123"));
        await _bus.Received(1).Publish(Arg.Any<PaymentCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Submitted")]
    [InlineData(null)]
    public async Task An_unfinished_payment_records_nothing(string? status)
    {
        var (_, attempt) = PendingPrompt();

        var outcome = await Settlement.ApplyAsync(attempt.Reference, "PD123", status, null, "test");

        Assert.Equal(SettlementOutcome.NotFinal, outcome);
        Assert.Equal(1, await _db.Payments.CountAsync());   // just the Pending row
        Assert.Null((await _db.MobileMoneyAttempts.SingleAsync()).ResolvedAt);
    }

    [Theory]
    [InlineData("testfp09")]                                   // FreshPay's sample: not ours
    [InlineData("00000000000000000000000000000000_abc")]       // our shape, but no such payment
    public async Task A_reference_that_isnt_ours_records_nothing(string reference)
    {
        PendingPrompt();

        var outcome = await Settlement.ApplyAsync(reference, "PD123", "Success", null, "test");

        Assert.Equal(SettlementOutcome.UnknownPayment, outcome);
        Assert.Equal(1, await _db.Payments.CountAsync());
    }

    [Fact]
    public async Task A_prompt_sent_before_attempts_were_recorded_is_found_from_its_reference()
    {
        var (payment, attempt) = PendingPrompt();
        _db.MobileMoneyAttempts.Remove(attempt);
        await _db.SaveChangesAsync();

        var outcome = await Settlement.ApplyAsync(attempt.Reference, "PD123", "Success", null, "test");

        Assert.Equal(SettlementOutcome.Recorded, outcome);
        Assert.Equal(payment.AppointmentId, (await _db.Payments.SingleAsync(p => p.ProviderRef == "PD123")).AppointmentId);
    }

    [Fact]
    public void Callbacks_in_the_documents_shape_are_read()
    {
        var debit = MokoCallback.From(JsonSerializer.Deserialize<JsonElement>(FreshPaySamples.DebitFailedCallback));
        var credit = MokoCallback.From(JsonSerializer.Deserialize<JsonElement>(FreshPaySamples.CreditSucceededCallback));

        Assert.Equal(new MokoCallback("testfp09", "PD9pLLM03QZJ08X7fXM242x6", "Failed",
            "La reference de la transaction est invalide, veuillez reesayez ou contactez le service client au 1213"), debit);
        Assert.Equal("Success", credit.TransStatus);
        Assert.Equal("PDABXkT03IfD08M9PfR24Ci4", credit.TransactionId);
    }
}
