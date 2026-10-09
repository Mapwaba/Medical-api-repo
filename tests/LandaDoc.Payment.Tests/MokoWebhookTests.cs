using System.Text;
using LandaDoc.Payment.Controllers;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Models;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LandaDoc.Payment.Tests;

// The callback endpoint: signed callbacks are trusted, plain ones only through FreshPay's verify
public class MokoWebhookTests
{
    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IPublishEndpoint _bus = Substitute.For<IPublishEndpoint>();
    private readonly IMokoAfrikaClient _moko = Substitute.For<IMokoAfrikaClient>();

    private async Task<IActionResult> PostAsync(string body, string? signature = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (signature is not null) http.Request.Headers["X-Signature"] = signature;
        var controller = new PaymentsController(_db, new ConfigurationBuilder().Build(), _bus, _moko, NullLogger<PaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        return await controller.MokoWebhook(new MobileMoneySettlement(_db, _bus, NullLogger<MobileMoneySettlement>.Instance), CancellationToken.None);
    }

    private string PendingPrompt()
    {
        var payment = new Models.Payment { AppointmentId = Guid.NewGuid(), GrossAmount = 50m, NetAmount = 45m, PlatformFee = 5m };
        var reference = $"{payment.Id:N}_{Guid.NewGuid():N}";
        _db.Payments.Add(payment);
        _db.MobileMoneyAttempts.Add(new MobileMoneyAttempt { PaymentId = payment.Id, Reference = reference });
        _db.SaveChanges();
        return reference;
    }

    // FreshPay's documented plain callback, about one of our payments
    private static string PlainCallback(string reference, string transStatus) =>
        FreshPaySamples.DebitFailedCallback.Replace("\"testfp09\"", $"\"{reference}\"").Replace("\"Trans_Status\": \"Failed\"", $"\"Trans_Status\": \"{transStatus}\"");

    [Fact]
    public async Task A_plain_callback_is_recorded_once_FreshPay_confirms_it()
    {
        var reference = PendingPrompt();
        _moko.VerifyAsync(reference, Arg.Any<CancellationToken>()).Returns(new MokoVerifyResult(true, "Success", "PD9pLLM03QZJ08X7fXM242x6", null));

        var result = await PostAsync(PlainCallback(reference, "Success"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PaymentStatus.Completed, (await _db.Payments.SingleAsync(p => p.ProviderRef == "PD9pLLM03QZJ08X7fXM242x6")).Status);
    }

    [Fact]
    public async Task A_plain_callback_that_lies_records_what_FreshPay_says()
    {
        var reference = PendingPrompt();
        _moko.VerifyAsync(reference, Arg.Any<CancellationToken>()).Returns(new MokoVerifyResult(true, "Failed", "PD9pLLM03QZJ08X7fXM242x6", "Annulé"));

        await PostAsync(PlainCallback(reference, "Success"));   // someone claims it was paid

        Assert.Equal(PaymentStatus.Failed, (await _db.Payments.SingleAsync(p => p.ProviderRef == "PD9pLLM03QZJ08X7fXM242x6")).Status);
    }

    [Fact]
    public async Task A_plain_callback_FreshPay_doesnt_know_records_nothing()
    {
        var reference = PendingPrompt();
        _moko.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new MokoVerifyResult(false, null, null, "not recognized"));

        var result = await PostAsync(PlainCallback(reference, "Success"));

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, await _db.Payments.CountAsync());   // just the Pending row
        Assert.Null((await _db.MobileMoneyAttempts.SingleAsync()).ResolvedAt);   // left for reconciliation
    }

    [Fact]
    public async Task A_plain_callback_for_someone_elses_reference_doesnt_reach_FreshPay()
    {
        PendingPrompt();

        var result = await PostAsync(FreshPaySamples.DebitFailedCallback);   // "testfp09"

        Assert.IsType<OkResult>(result);
        await _moko.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
    }

    [Fact]
    public async Task An_encrypted_callback_without_a_valid_signature_is_refused()
    {
        _moko.VerifySignature(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Assert.IsType<UnauthorizedResult>(await PostAsync("""{"data":"abc"}"""));
        Assert.IsType<UnauthorizedResult>(await PostAsync("""{"data":"abc"}""", signature: "forged"));
    }

    [Fact]
    public async Task A_signed_callback_is_recorded_as_it_says()
    {
        var reference = PendingPrompt();
        _moko.VerifySignature("abc", "good").Returns(true);
        _moko.Decrypt("abc").Returns(PlainCallback(reference, "Success"));

        await PostAsync("""{"data":"abc"}""", signature: "good");

        Assert.Equal(PaymentStatus.Completed, (await _db.Payments.SingleAsync(p => p.ProviderRef == "PD9pLLM03QZJ08X7fXM242x6")).Status);
        await _moko.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"Trans_Status":"Success"}""")]
    public async Task Nonsense_is_refused(string body) =>
        Assert.IsType<BadRequestResult>(await PostAsync(body));
}
