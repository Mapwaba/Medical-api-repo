using System.Text.Json;

namespace LandaDoc.Payment.Services;

// The fields LandaDoc uses from a FreshPay callback (plain, or decrypted from a signed one):
// {"Reference": ours, "PayDRC_Reference"/"Transaction_id": FreshPay's, "Trans_Status": the outcome,
//  "Trans_Status_Description": the operator's message}
public record MokoCallback(string? Reference, string? TransactionId, string? TransStatus, string? Description)
{
    public static MokoCallback From(JsonElement payload) => new(
        Text(payload, "Reference"),
        Text(payload, "Transaction_id") ?? Text(payload, "PayDRC_Reference"),
        Text(payload, "Trans_Status"),
        Text(payload, "Trans_Status_Description"));

    private static string? Text(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;
}
