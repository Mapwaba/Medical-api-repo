using LandaDoc.Payment.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Services;

// What LandaDoc owes each doctor, worked out from the ledger every time (nothing is stored, so it
// can't drift from the payments it's based on):
//   + the doctor's share (NetAmount) of every completed card / mobile money payment
//   - LandaDoc's fee on insurance claims the insurer has paid (the insurer paid the doctor the
//     full amount directly, so LandaDoc's share comes out of the doctor's balance)
//   - every payout already recorded
public class DoctorBalances(PaymentDbContext db)
{
    public async Task<DoctorBalanceDto> ForDoctorAsync(Guid doctorId) =>
        (await ComputeAsync(doctorId)).GetValueOrDefault(doctorId) ?? Empty(doctorId, await AccountAsync(doctorId));

    // Every doctor with money in or out, or a payout account
    public async Task<List<DoctorBalanceDto>> AllAsync() => (await ComputeAsync(null)).Values.ToList();

    private async Task<Dictionary<Guid, DoctorBalanceDto>> ComputeAsync(Guid? onlyDoctor)
    {
        var payments = db.Payments.Where(p => onlyDoctor == null || p.DoctorId == onlyDoctor);

        var earned = await payments
            .Where(p => p.Status == PaymentStatus.Completed && p.Provider != PaymentProvider.Insurance && p.Provider != null)
            .GroupBy(p => p.DoctorId)
            .Select(g => new { DoctorId = g.Key, Amount = g.Sum(p => p.NetAmount) })
            .ToDictionaryAsync(x => x.DoctorId, x => x.Amount);

        // An approved claim's ledger row has ProviderRef "claim_<claim id without dashes>"
        var settledRefs = await db.InsuranceClaims
            .Where(c => c.Status == InsuranceClaimStatus.Settled && (onlyDoctor == null || c.DoctorId == onlyDoctor))
            .Select(c => c.Id)
            .ToListAsync();
        var refs = settledRefs.Select(id => $"claim_{id:N}").ToList();
        var insuranceFees = await payments
            .Where(p => p.Status == PaymentStatus.Completed && p.Provider == PaymentProvider.Insurance && refs.Contains(p.ProviderRef!))
            .GroupBy(p => p.DoctorId)
            .Select(g => new { DoctorId = g.Key, Amount = g.Sum(p => p.PlatformFee) })
            .ToDictionaryAsync(x => x.DoctorId, x => x.Amount);

        var payouts = await db.DoctorPayouts
            .Where(p => onlyDoctor == null || p.DoctorId == onlyDoctor)
            .GroupBy(p => p.DoctorId)
            .Select(g => new { DoctorId = g.Key, Amount = g.Sum(p => p.Amount), Last = g.Max(p => p.PaidAt) })
            .ToDictionaryAsync(x => x.DoctorId);

        var accounts = await db.DoctorPayoutAccounts
            .Where(a => onlyDoctor == null || a.DoctorId == onlyDoctor)
            .ToDictionaryAsync(a => a.DoctorId);

        var doctors = earned.Keys.Concat(insuranceFees.Keys).Concat(payouts.Keys).Concat(accounts.Keys).Distinct();
        return doctors.ToDictionary(id => id, id =>
        {
            var e = earned.GetValueOrDefault(id);
            var f = insuranceFees.GetValueOrDefault(id);
            payouts.TryGetValue(id, out var paid);
            accounts.TryGetValue(id, out var account);
            var paidOut = paid?.Amount ?? 0;
            return new DoctorBalanceDto(id, e, f, paidOut, e - f - paidOut, paid?.Last, account is null ? null : ToDto(account));
        });
    }

    private async Task<PayoutAccountDto?> AccountAsync(Guid doctorId)
    {
        var account = await db.DoctorPayoutAccounts.FindAsync(doctorId);
        return account is null ? null : ToDto(account);
    }

    private static DoctorBalanceDto Empty(Guid doctorId, PayoutAccountDto? account) => new(doctorId, 0, 0, 0, 0, null, account);

    public static PayoutAccountDto ToDto(Models.DoctorPayoutAccount a) => new(
        a.DoctorId, a.Method, a.AccountName, a.Operator, a.MobileNumber, a.BankName, a.BankAccountNumber,
        a.IsVerified, a.VerifiedAt, a.UpdatedAt);

    // "Airtel Money +243 81 000 0000 (Jane Doe)" / "Rawbank 00011-... (Jane Doe)", kept on each payout
    public static string Describe(Models.DoctorPayoutAccount a) => a.Method == PayoutMethod.MobileMoney
        ? $"{OperatorName(a.Operator)} {a.MobileNumber} ({a.AccountName})"
        : $"{a.BankName} {a.BankAccountNumber} ({a.AccountName})";

    private static string OperatorName(MobileMoneyOperator? op) => op switch
    {
        MobileMoneyOperator.Airtel => "Airtel Money",
        MobileMoneyOperator.Orange => "Orange Money",
        MobileMoneyOperator.Mpesa => "M-Pesa",
        MobileMoneyOperator.Africell => "Africell Money",
        _ => "Mobile Money",
    };
}
