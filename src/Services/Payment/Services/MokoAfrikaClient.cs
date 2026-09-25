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
            ["method"] = operatorMethod.ToString().ToLowerInvariant(),
            ["amount"] = amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["currency"] = "USD",
            ["customer_number"] = NormalizePhoneNumber(phoneNumber),
            ["reference"] = reference,
            ["firstname"] = firstName,
            ["lastname"] = lastName,
            ["email"] = email,
            ["callback_url"] = callbackUrl
        };

        var response = await http.PostAsJsonAsync("", payload);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var status = body.TryGetProperty("Status", out var s) ? s.GetString() : null;
        var comment = body.TryGetProperty("Comment", out var c) ? c.GetString() : null;
        var transactionId = body.TryGetProperty("Transaction_id", out var t) ? t.GetString() : null;
        return new MokoDebitResult(response.IsSuccessStatusCode && status == "Success", transactionId, comment);
    }

    public async Task<string?> VerifyAsync(string reference)
    {
        var payload = new Dictionary<string, string?>
        {
            ["merchant_id"] = _options.MerchantId,
            ["merchant_secrete"] = _options.MerchantSecret,
            ["action"] = "verify",
            ["reference"] = reference
        };

        var response = await http.PostAsJsonAsync("", payload);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("Trans_Status", out var t) ? t.GetString() : null;
    }

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
    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("243", StringComparison.Ordinal)) return digits;
        return digits.StartsWith('0') ? "243" + digits[1..] : "243" + digits;
    }
}
