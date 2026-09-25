using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Services;

public record MokoDebitResult(bool Success, string? TransactionId, string? Comment);

public interface IMokoAfrikaClient
{
    Task<MokoDebitResult> InitiateDebitAsync(
        string reference, decimal amount, string phoneNumber, MobileMoneyOperator operatorMethod,
        string firstName, string lastName, string email, string callbackUrl);

    // Not wired to a public endpoint yet — for a future reconciliation job / support lookup.
    Task<string?> VerifyAsync(string reference);

    // Callback verification — the encrypted `data` field's HMAC-SHA256 signature and its
    // AES-CBC decryption, both keyed off MokoAfrikaOptions.
    bool VerifySignature(string encryptedData, string signature);
    string Decrypt(string encryptedData);
}
