using LandaDoc.Shared.Models;

namespace LandaDoc.Payment.Models;

// A patient's request to have a booking paid by their insurer. Unlike the payments ledger
// this row is updated in place as it moves through review and reconciliation; the ledger
// only gets a Completed row (Provider = Insurance) once the doctor approves the claim.
public class InsuranceClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }          // the Pending ledger row the claim pays for
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public Guid InsurerId { get; set; }
    public string InsurerName { get; set; } = "";  // as it was when the claim was made
    public string MemberNumber { get; set; } = "";
    public string? MemberName { get; set; }
    public decimal Amount { get; set; }
    public InsuranceClaimStatus Status { get; set; } = InsuranceClaimStatus.Submitted;
    public string? Note { get; set; }              // doctor's reason for declining / rejection
    public string? InsurerReference { get; set; }  // insurer's payment reference once settled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
