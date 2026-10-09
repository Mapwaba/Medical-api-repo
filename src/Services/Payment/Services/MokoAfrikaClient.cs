using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LandaDoc.Shared.Models;
using Microsoft.Extensions.Options;

namespace LandaDoc.Payment.Services;

// Confirmed against a real sandbox request (not just the public doc site, which turned out
// to describe a different host/version and an extra token step that the real API doesn't
// have): merchant_id + merchant_secrete travel in the JSON body of every call to the single
// gateway endpoint — no separate token exchange, no Authorization header.
public class MokoAfrikaClient(HttpClient http, IOptions<MokoAfrikaOptions> options) : IMokoAfrikaClient
{
    private readonly MokoAfrikaOptions _options = options.Value;

    public async Task<MokoDebitResult> InitiateDebitAsync(
        string reference, decimal amount, string phoneNumber, MobileMoneyOperator operatorMethod,
        string firstName, string lastName, string email, string callbackUrl)
    {
        var payload = new Dictionary<string, string?>
        {
            ["merchant_id"] = _options.MerchantId,
            ["merchant_secrete"] = _options.MerchantSecret,
            ["action"] = "debit",
            ["method"] = MethodName(operatorMethod),
            ["amount"] = amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["currency"] = "USD",
            ["customer_number"] = NormalizePhoneNumber(phoneNumber),
            ["reference"] = reference,
            ["firstname"] = firstName,
            ["lastname"] = lastName,
            // FreshPay's PayDRC document spells it "e-mail"; the sandbox request this client was
            // first checked against used "email". Both are sent until FreshPay confirms which.
            ["email"] = email,
            ["e-mail"] = email,
            ["callback_url"] = callbackUrl
        };

        var response = await http.PostAsJsonAsync("", payload);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var status = body.TryGetProperty("Status", out var s) ? s.GetString() : null;
        var comment = body.TryGetProperty("Comment", out var c) ? c.GetString() : null;
        var transactionId = body.TryGetProperty("Transaction_id", out var t) ? t.GetString() : null;
        return new MokoDebitResult(response.IsSuccessStatusCode && status == "Success", transactionId, comment);
    }

    // FreshPay replies {"Status":"Success","Comment":"Transaction Found","Trans_Status":..., "Transaction_id":...}
    // when it knows the transaction, and {"Status":"Error","resultCodeError":404,...} when it doesn't.
    // Errors are read from the body whatever the HTTP status, since FreshPay uses both.
    public async Task<MokoVerifyResult> VerifyAsync(string reference, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, string?>
        {
            ["merchant_id"] = _options.MerchantId,
            ["merchant_secrete"] = _options.MerchantSecret,
            ["action"] = "verify",
            ["reference"] = reference
        };

        JsonElement body;
        try
        {
            var response = await http.PostAsJsonAsync("", payload, ct);
            body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new MokoVerifyResult(false, null, null, ex.GetBaseException().Message);
        }

        if (body.ValueKind != JsonValueKind.Object || !string.Equals(Text(body, "Status"), "Success", StringComparison.OrdinalIgnoreCase))
            return new MokoVerifyResult(false, null, null, Text(body, "Comment") ?? Text(body, "resultCodeErrorDescription"));

        return new MokoVerifyResult(true,
            Text(body, "Trans_Status"),
            Text(body, "Transaction_id") ?? Text(body, "PayDRC_Reference"),
            Text(body, "Trans_Status_Description"));
    }

    // FreshPay's "method" values: airtel, orange, mpesa, afrimoney (Africell's service)
    public static string MethodName(MobileMoneyOperator op) => op switch
    {
        MobileMoneyOperator.Africell => "afrimoney",
        _ => op.ToString().ToLowerInvariant(),
    };

    private static string? Text(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public bool VerifySignature(string encryptedData, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.HmacKey));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(encryptedData));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public string Decrypt(string encryptedData)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedData);
        var keyBytes = Encoding.UTF8.GetBytes(_options.AesKey);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = keyBytes; // per Moko Afrika's documented scheme — IV equals the AES key
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    // The confirmed sandbox sample sends the number as "243970000000" — 243 prefix, no
    // leading 0 — so normalize toward that shape rather than reject other formats.
    public static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("243", StringComparison.Ordinal)) return digits;
        return digits.StartsWith('0') ? "243" + digits[1..] : "243" + digits;
    }
}
