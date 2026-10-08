using System.Security.Claims;
using LandaDoc.Payment.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Controllers;

// Insurance claims are filed by the patient through POST /api/payments/{id}/initiate
// (Provider = Insurance). From there the doctor reviews them (approve / decline) and later
// reconciles them with the insurer (settled / rejected).
[ApiController]
[Route("api/insurance-claims")]
public class InsuranceClaimsController(PaymentDbContext db, IPublishEndpoint bus) : ControllerBase
{
    // Every claim on the calling doctor's appointments, newest first
    [HttpGet("doctor/me")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> GetMineAsDoctor()
    {
        var callerId = CallerId();
        var claims = await db.InsuranceClaims
            .Where(c => c.DoctorId == callerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return Ok(claims.Select(MapToDto));
    }

    // The latest claim for an appointment (a declined claim can be followed by a new one)
    [HttpGet("by-appointment/{appointmentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetByAppointment(Guid appointmentId)
    {
        var claim = await db.InsuranceClaims
            .Where(c => c.AppointmentId == appointmentId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        if (claim is null) return NotFound();

        var callerId = CallerId();
        if (claim.PatientId != callerId && claim.DoctorId != callerId) return Forbid();

        return Ok(MapToDto(claim));
    }

    // The doctor accepts the patient's cover: the booking counts as paid (insurer pays later)
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var claim = await db.InsuranceClaims.FindAsync(id);
        if (claim is null) return NotFound();
        if (claim.DoctorId != CallerId()) return Forbid();
        if (claim.Status != InsuranceClaimStatus.Submitted)
            return Conflict(new { error = "Only submitted claims can be approved" });

        // The booking must still be waiting for payment (not expired, not paid another way)
        var latest = await db.Payments
            .Where(p => p.AppointmentId == claim.AppointmentId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstAsync();
        if (latest.Status != PaymentStatus.Pending)
            return Conflict(new { error = "This booking is no longer waiting for payment" });

        claim.Status = InsuranceClaimStatus.Approved;
        claim.UpdatedAt = DateTime.UtcNow;

        // Insert a new Completed row (immutable ledger — no updates)
        db.Payments.Add(new Models.Payment
        {
            AppointmentId = latest.AppointmentId,
            DoctorId = latest.DoctorId,
            PatientId = latest.PatientId,
            GrossAmount = latest.GrossAmount,
            PlatformFee = latest.PlatformFee,
            NetAmount = latest.NetAmount,
            Status = PaymentStatus.Completed,
            Provider = PaymentProvider.Insurance,
            ProviderRef = $"claim_{claim.Id:N}",
        });
        await db.SaveChangesAsync();

        await bus.Publish(new PaymentCompletedEvent(
            claim.AppointmentId, claim.DoctorId, claim.PatientId, DateTime.UtcNow, PaymentProvider.Insurance));
        return Ok(MapToDto(claim));
    }

    // The doctor won't take this cover: the patient can pay another way
    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Decline(Guid id, [FromBody] ClaimActionRequest req)
    {
        var claim = await db.InsuranceClaims.FindAsync(id);
        if (claim is null) return NotFound();
        if (claim.DoctorId != CallerId()) return Forbid();
        if (claim.Status != InsuranceClaimStatus.Submitted)
            return Conflict(new { error = "Only submitted claims can be declined" });

        claim.Status = InsuranceClaimStatus.Declined;
        claim.Note = Clean(req.Note);
        claim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await bus.Publish(new InsuranceClaimDeclinedEvent(
            claim.AppointmentId, claim.DoctorId, claim.PatientId, claim.Note, DateTime.UtcNow));
        return Ok(MapToDto(claim));
    }

    // Reconciliation: the insurer paid the claim
    [HttpPost("{id:guid}/settle")]
    [Authorize(Roles = "Doctor")]
    public Task<IActionResult> Settle(Guid id, [FromBody] ClaimActionRequest req) =>
        ReconcileAsync(id, InsuranceClaimStatus.Settled, req);

    // Reconciliation: the insurer refused to pay. The booking stays confirmed — collecting
    // from the patient directly is between the doctor and the patient.
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Doctor")]
    public Task<IActionResult> Reject(Guid id, [FromBody] ClaimActionRequest req) =>
        ReconcileAsync(id, InsuranceClaimStatus.Rejected, req);

    private async Task<IActionResult> ReconcileAsync(Guid id, InsuranceClaimStatus outcome, ClaimActionRequest req)
    {
        var claim = await db.InsuranceClaims.FindAsync(id);
        if (claim is null) return NotFound();
        if (claim.DoctorId != CallerId()) return Forbid();
        if (claim.Status != InsuranceClaimStatus.Approved)
            return Conflict(new { error = "Only approved claims can be reconciled" });

        claim.Status = outcome;
        if (outcome == InsuranceClaimStatus.Settled) claim.InsurerReference = Clean(req.Note);
        else claim.Note = Clean(req.Note);
        claim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapToDto(claim));
    }

    private Guid CallerId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static InsuranceClaimDto MapToDto(Models.InsuranceClaim c) => new(
        c.Id, c.PaymentId, c.AppointmentId, c.DoctorId, c.PatientId,
        c.InsurerId, c.InsurerName, c.MemberNumber, c.MemberName,
        c.Amount, c.Status, c.Note, c.InsurerReference, c.CreatedAt, c.UpdatedAt);
}
