using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Services;

public record MokoDebitResult(bool Success, string? TransactionId, string? Comment);

// What FreshPay's "verify" action says about a transaction. Found is false when FreshPay doesn't
// know the reference (or couldn't be reached). TransStatus is the payment's real outcome
// ("Success", "Failed", or something still in progress); Status in FreshPay's reply only says the
// lookup itself worked.
public record MokoVerifyResult(bool Found, string? TransStatus, string? TransactionId, string? Description)
{
    public bool IsFinal => MokoStatus.IsFinal(TransStatus);
}

public interface IMokoAfrikaClient
{
    Task<MokoDebitResult> InitiateDebitAsync(
        string reference, decimal amount, string phoneNumber, MobileMoneyOperator operatorMethod,
        string firstName, string lastName, string email, string callbackUrl);

    // Asks FreshPay where a transaction stands, by our reference or FreshPay's "PD…" id.
    // Used to trust plain (unsigned) callbacks and by the reconciliation job.
    Task<MokoVerifyResult> VerifyAsync(string reference, CancellationToken ct = default);

    // Callback verification — the encrypted `data` field's HMAC-SHA256 signature and its
    // AES-CBC decryption, both keyed off MokoAfrikaOptions.
    bool VerifySignature(string encryptedData, string signature);
    string Decrypt(string encryptedData);
}

// FreshPay's final transaction statuses (Trans_Status); anything else is still in progress
public static class MokoStatus
{
    public static bool IsSuccess(string? transStatus) => string.Equals(transStatus, "Success", StringComparison.OrdinalIgnoreCase);
    public static bool IsFailure(string? transStatus) => string.Equals(transStatus, "Failed", StringComparison.OrdinalIgnoreCase);
    public static bool IsFinal(string? transStatus) => IsSuccess(transStatus) || IsFailure(transStatus);
}
