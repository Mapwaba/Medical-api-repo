using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Models;

// One mobile money prompt sent through Moko Afrika (FreshPay). The ledger only learns the outcome
// when FreshPay calls back, so the prompt is remembered here: the reconciliation job asks FreshPay
// about attempts still unresolved (a callback that never arrived, e.g. while this service slept),
// and plain-JSON callbacks are matched to the payment through it.
public class MobileMoneyAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }          // the Pending ledger row being paid
    public string Reference { get; set; } = "";  // ours, sent to FreshPay: "<payment id N>_<guid N>"
    public string? ProviderTransactionId { get; set; } // FreshPay's "PD…" id from its first reply
    public MobileMoneyOperator Operator { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastCheckedAt { get; set; } // last time the reconciliation job asked FreshPay
    public DateTime? ResolvedAt { get; set; }    // set once a Completed/Failed row was recorded
    public string? Outcome { get; set; }         // FreshPay's final Trans_Status ("Success"/"Failed")
}
