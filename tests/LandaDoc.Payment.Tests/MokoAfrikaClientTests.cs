using System.Net;
using System.Text;
using System.Text.Json;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.Models;
using Microsoft.Extensions.Options;

namespace LandaDoc.Payment.Tests;

public class MokoAfrikaClientTests
{
    // Answers every request with one canned reply and keeps the request body
    private sealed class CannedHandler(HttpStatusCode status, string reply) : HttpMessageHandler
    {
        public JsonElement LastRequest { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastRequest = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(status) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("FreshPay unreachable");
    }

    private static MokoAfrikaClient Client(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://freshpay.test/api/v5/") },
        Options.Create(new MokoAfrikaOptions { MerchantId = "merchant", MerchantSecret = "secret" }));

    private static string Field(JsonElement request, string name) => request.GetProperty(name).GetString()!;

    [Fact]
    public async Task Debit_request_has_the_fields_the_document_lists()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, FreshPaySamples.RequestReceived);

        await Client(handler).InitiateDebitAsync("ref_1", 25m, "0972148867", MobileMoneyOperator.Airtel,
            "Ruth", "Mbuyi", "ruth@example.com", "https://landadoc.test/api/payments/webhook/moko");

        var sent = handler.LastRequest;
        Assert.Equal("merchant", Field(sent, "merchant_id"));
        Assert.Equal("secret", Field(sent, "merchant_secrete"));
        Assert.Equal("debit", Field(sent, "action"));
        Assert.Equal("airtel", Field(sent, "method"));
        Assert.Equal("25", Field(sent, "amount"));
        Assert.Equal("USD", Field(sent, "currency"));
        Assert.Equal("243972148867", Field(sent, "customer_number"));
        Assert.Equal("Ruth", Field(sent, "firstname"));
        Assert.Equal("Mbuyi", Field(sent, "lastname"));
        Assert.Equal("ruth@example.com", Field(sent, "e-mail"));   // the document's spelling
        Assert.Equal("ruth@example.com", Field(sent, "email"));    // the sandbox's
        Assert.Equal("ref_1", Field(sent, "reference"));
        Assert.Equal("https://landadoc.test/api/payments/webhook/moko", Field(sent, "callback_url"));
    }

    [Theory]
    [InlineData(MobileMoneyOperator.Airtel, "airtel")]
    [InlineData(MobileMoneyOperator.Orange, "orange")]
    [InlineData(MobileMoneyOperator.Mpesa, "mpesa")]
    [InlineData(MobileMoneyOperator.Africell, "afrimoney")]
    public void Operators_use_FreshPays_method_names(MobileMoneyOperator op, string method) =>
        Assert.Equal(method, MokoAfrikaClient.MethodName(op));

    [Theory]
    [InlineData("0972148867", "243972148867")]
    [InlineData("+243 97 214 8867", "243972148867")]
    [InlineData("243972148867", "243972148867")]
    [InlineData("972148867", "243972148867")]
    public void Phone_numbers_start_with_243(string typed, string sent) =>
        Assert.Equal(sent, MokoAfrikaClient.NormalizePhoneNumber(typed));

    [Fact]
    public async Task Acknowledged_debit_keeps_FreshPays_transaction_id()
    {
        var result = await Client(new CannedHandler(HttpStatusCode.OK, FreshPaySamples.RequestReceived))
            .InitiateDebitAsync("testfp09", 100m, "0972148867", MobileMoneyOperator.Airtel, "A", "B", "a@b.c", "https://cb");

        Assert.True(result.Success);
        Assert.Equal("PDABXkT03IfD08M9PfR24Ci4", result.TransactionId);
    }

    [Fact]
    public async Task Refused_debit_carries_FreshPays_reason()
    {
        var result = await Client(new CannedHandler(HttpStatusCode.BadRequest, FreshPaySamples.RequestRefused))
            .InitiateDebitAsync("testfp09", 100m, "12345", MobileMoneyOperator.Airtel, "A", "B", "a@b.c", "https://cb");

        Assert.False(result.Success);
        Assert.Equal("Customer number is incorrect, be sure to start with 243", result.Comment);
    }

    [Fact]
    public async Task Verify_reads_the_real_outcome_from_Trans_Status()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, FreshPaySamples.VerifyFound);

        var result = await Client(handler).VerifyAsync("testfp09");

        Assert.Equal("verify", Field(handler.LastRequest, "action"));
        Assert.Equal("testfp09", Field(handler.LastRequest, "reference"));
        Assert.True(result.Found);
        Assert.Equal("Failed", result.TransStatus);   // the lookup's own "Status" is "Success"
        Assert.True(result.IsFinal);
        Assert.Equal("PD9pLLM03QZJ08X7fXM242x6", result.TransactionId);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Verify_of_an_unknown_reference_is_not_found(HttpStatusCode status)
    {
        var result = await Client(new CannedHandler(status, FreshPaySamples.VerifyNotFound)).VerifyAsync("nope");

        Assert.False(result.Found);
        Assert.False(result.IsFinal);
    }

    [Fact]
    public async Task Verify_when_FreshPay_is_unreachable_is_not_found()
    {
        var result = await Client(new FailingHandler()).VerifyAsync("testfp09");

        Assert.False(result.Found);
        Assert.Equal("FreshPay unreachable", result.Description);
    }

    [Fact]
    public async Task Verify_when_FreshPay_answers_something_else_than_json_is_not_found()
    {
        var result = await Client(new CannedHandler(HttpStatusCode.BadGateway, "<html>Bad gateway</html>")).VerifyAsync("testfp09");

        Assert.False(result.Found);
    }
}
