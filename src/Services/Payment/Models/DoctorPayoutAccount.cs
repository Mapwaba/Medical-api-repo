using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Models;

// Where a doctor wants their earnings sent. The doctor fills it in; an admin checks it (e.g. by
// confirming the number with the doctor) before any payout. Editing it clears the check.
public class DoctorPayoutAccount
{
    public Guid DoctorId { get; set; }
    public PayoutMethod Method { get; set; }
    public string AccountName { get; set; } = "";   // the name the account is registered under
    public MobileMoneyOperator? Operator { get; set; }
    public string? MobileNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByAdminId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
