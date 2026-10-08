namespace LandaDoc.Payment.Models;

// Money LandaDoc sent a doctor, recorded by the admin who sent it. Never edited or deleted:
// a mistake is corrected by recording another payout (or by the next one being smaller).
public class DoctorPayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public decimal Amount { get; set; }
    public string PaidTo { get; set; } = "";       // the account as it was, e.g. "Airtel Money +243 81..."
    public string? Reference { get; set; }         // the operator's or bank's transaction reference
    public string? Note { get; set; }
    public Guid RecordedByAdminId { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}
