using System.Security.Claims;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Controllers;

// Paying doctors their earnings. Doctors give the account to be paid on and see their balance
// and payouts; admins check the account, send the money outside the app (Mobile Money or bank
// transfer) and record each payout here. See DoctorBalances for how a balance is worked out.
[ApiController]
[Route("api/payouts")]
public class PayoutsController(PaymentDbContext db, DoctorBalances balances, IPublishEndpoint bus, ILogger<PayoutsController> log) : ControllerBase
{
    // ── Doctor ──────────────────────────────────────────────────────

    [HttpGet("me")]
    [Authorize(Roles = "Doctor")]
    public Task<IActionResult> GetMine() => SummaryAsync(CallerId());

    [HttpPut("me/account")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> SaveMyAccount([FromBody] SavePayoutAccountRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (req.Method == PayoutMethod.MobileMoney && (req.Operator is null || string.IsNullOrWhiteSpace(req.MobileNumber)))
            return BadRequest(new { error = "Choose the operator and enter the mobile money number" });
        if (req.Method == PayoutMethod.Bank && (string.IsNullOrWhiteSpace(req.BankName) || string.IsNullOrWhiteSpace(req.BankAccountNumber)))
            return BadRequest(new { error = "Enter the bank and the account number" });

        var doctorId = CallerId();
        var account = await db.DoctorPayoutAccounts.FindAsync(doctorId);
        if (account is null)
        {
            account = new Models.DoctorPayoutAccount { DoctorId = doctorId };
            db.DoctorPayoutAccounts.Add(account);
        }
        var mobile = req.Method == PayoutMethod.MobileMoney;
        account.Method = req.Method;
        account.AccountName = req.AccountName.Trim();
        account.Operator = mobile ? req.Operator : null;
        account.MobileNumber = mobile ? req.MobileNumber!.Trim() : null;
        account.BankName = mobile ? null : req.BankName!.Trim();
        account.BankAccountNumber = mobile ? null : req.BankAccountNumber!.Trim();
        // New details must be checked again before money goes there
        account.IsVerified = false;
        account.VerifiedAt = null;
        account.VerifiedByAdminId = null;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        log.LogInformation("Doctor {DoctorId} saved a {Method} payout account; waiting for an admin to check it", doctorId, req.Method);
        return Ok(DoctorBalances.ToDto(account));
    }

    // ── Admin ───────────────────────────────────────────────────────

    // Every doctor with a balance, a payout or a payout account, most owed first
    [HttpGet("balances")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetBalances() =>
        Ok((await balances.AllAsync()).OrderByDescending(b => b.Balance).ToList());

    [HttpGet("doctor/{doctorId:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> GetForDoctor(Guid doctorId) => SummaryAsync(doctorId);

    // LandaDoc's wallets at Moko Afrika (FreshPay): collected payments, and what can be paid out
    [HttpGet("mobile-money-wallets")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetMobileMoneyWallets([FromServices] IMokoAfrikaClient moko, CancellationToken ct)
    {
        var wallets = await moko.GetBalancesAsync(ct);
        if (wallets is null)
        {
            log.LogWarning("Moko Afrika balances couldn't be read (unreachable, or credentials refused)");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Moko Afrika didn't return the balances" });
        }
        return Ok(wallets);
    }

    // The admin has confirmed the account really is the doctor's
    [HttpPost("doctor/{doctorId:guid}/account/verify")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerifyAccount(Guid doctorId)
    {
        var account = await db.DoctorPayoutAccounts.FindAsync(doctorId);
        if (account is null) return NotFound();

        account.IsVerified = true;
        account.VerifiedAt = DateTime.UtcNow;
        account.VerifiedByAdminId = CallerId();
        await db.SaveChangesAsync();
        log.LogInformation("Payout account of doctor {DoctorId} checked by admin {AdminId}", doctorId, account.VerifiedByAdminId);
        return Ok(DoctorBalances.ToDto(account));
    }

    // The admin has sent the doctor money: record it. Only to a checked account, and never more
    // than the doctor is owed.
    [HttpPost("doctor/{doctorId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecordPayout(Guid doctorId, [FromBody] RecordPayoutRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var account = await db.DoctorPayoutAccounts.FindAsync(doctorId);
        if (account is null || !account.IsVerified)
            return Conflict(new { error = "The doctor's payout account must be checked first", code = "account" });

        var balance = await balances.ForDoctorAsync(doctorId);
        var amount = Math.Round(req.Amount, 2);
        if (amount > balance.Balance)
            return Conflict(new { error = $"The doctor is owed {balance.Balance:0.00} at most", code = "amount" });

        var payout = new Models.DoctorPayout
        {
            DoctorId = doctorId,
            Amount = amount,
            PaidTo = DoctorBalances.Describe(account),
            Reference = Clean(req.Reference),
            Note = Clean(req.Note),
            RecordedByAdminId = CallerId(),
        };
        db.DoctorPayouts.Add(payout);
        await db.SaveChangesAsync();

        await bus.Publish(new DoctorPayoutRecordedEvent(payout.Id, doctorId, payout.Amount, payout.PaidTo, payout.Reference, DateTime.UtcNow));
        log.LogInformation("Payout {PayoutId} of {Amount} to doctor {DoctorId} recorded by admin {AdminId} (ref {Reference})",
            payout.Id, payout.Amount, doctorId, payout.RecordedByAdminId, payout.Reference);
        return Ok(ToDto(payout));
    }

    private async Task<IActionResult> SummaryAsync(Guid doctorId)
    {
        var balance = await balances.ForDoctorAsync(doctorId);
        var payouts = await db.DoctorPayouts
            .Where(p => p.DoctorId == doctorId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();
        return Ok(new DoctorPayoutsDto(balance, payouts.Select(ToDto).ToList()));
    }

    private Guid CallerId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PayoutDto ToDto(Models.DoctorPayout p) => new(p.Id, p.DoctorId, p.Amount, p.PaidTo, p.Reference, p.Note, p.PaidAt);
}
