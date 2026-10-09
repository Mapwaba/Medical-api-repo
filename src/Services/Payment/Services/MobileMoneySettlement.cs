using LandaDoc.Payment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Services;

public enum SettlementOutcome
{
    Recorded,        // a Completed or Failed row was added to the ledger
    AlreadyRecorded, // this transaction was recorded before (callback redelivered, or the job got there first)
    NotFinal,        // FreshPay hasn't finished with it yet
    UnknownPayment,  // the reference doesn't point to a payment of ours
}

// Records the final result of a mobile money prompt: one Completed or Failed ledger row, the
// matching event, and the attempt marked resolved. Shared by the signed callback, plain callbacks
// (once FreshPay's verify confirmed them) and the reconciliation job, so a payment is recorded
// once whichever of them gets there first.
public class MobileMoneySettlement(PaymentDbContext db, IPublishEndpoint bus, ILogger<MobileMoneySettlement> log)
{
    public async Task<SettlementOutcome> ApplyAsync(
        string reference, string? transactionId, string? transStatus, string? description, string source, CancellationToken ct = default)
    {
        if (!MokoStatus.IsFinal(transStatus)) return SettlementOutcome.NotFinal;

        var attempt = await db.MobileMoneyAttempts.FirstOrDefaultAsync(a => a.Reference == reference, ct);
        var paymentId = attempt?.PaymentId ?? PaymentIdFrom(reference);
        if (paymentId is null) return SettlementOutcome.UnknownPayment;

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return SettlementOutcome.UnknownPayment;

        // The ledger row is keyed by FreshPay's transaction id (unique), or our reference without one
        var providerRef = string.IsNullOrWhiteSpace(transactionId) ? reference : transactionId;
        var succeeded = MokoStatus.IsSuccess(transStatus);

        if (attempt?.ResolvedAt is not null || await db.Payments.AnyAsync(p => p.ProviderRef == providerRef, ct))
        {
            await MarkResolvedAsync(attempt, transStatus, ct);
            return SettlementOutcome.AlreadyRecorded;
        }

        // Insert a new terminal row (immutable ledger — no updates)
        db.Payments.Add(new Models.Payment
        {
            AppointmentId = payment.AppointmentId,
            DoctorId = payment.DoctorId,
            PatientId = payment.PatientId,
            BookedByUserId = payment.BookedByUserId,
            GrossAmount = payment.GrossAmount,
            PlatformFee = payment.PlatformFee,
            NetAmount = payment.NetAmount,
            Status = succeeded ? PaymentStatus.Completed : PaymentStatus.Failed,
            Provider = PaymentProvider.MokoAfrika,
            ProviderRef = providerRef,
        });
        if (attempt is not null)
        {
            attempt.ResolvedAt = DateTime.UtcNow;
            attempt.Outcome = transStatus;
            attempt.ProviderTransactionId ??= transactionId;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The callback and the reconciliation job raced and the other one inserted the row
            // first (the unique ProviderRef index refused this one)
            db.ChangeTracker.Clear();
            return SettlementOutcome.AlreadyRecorded;
        }

        if (succeeded)
        {
            await bus.Publish(new PaymentCompletedEvent(
                payment.AppointmentId, payment.DoctorId, payment.PatientId, DateTime.UtcNow), ct);
            log.LogInformation("Mobile money payment succeeded for appointment {AppointmentId}: {Amount} (Moko {ProviderRef}, from {Source})",
                payment.AppointmentId, payment.GrossAmount, providerRef, source);
        }
        else
        {
            await bus.Publish(new PaymentFailedEvent(payment.AppointmentId, description, DateTime.UtcNow), ct);
            log.LogWarning("Mobile money payment failed for appointment {AppointmentId} (Moko {ProviderRef}, from {Source}): {Reason}",
                payment.AppointmentId, providerRef, source, description);
        }
        return SettlementOutcome.Recorded;
    }

    // Our reference is "<payment id, N format>_<attempt guid>", so a callback can be traced back to
    // its payment even for prompts sent before attempts were recorded
    public static Guid? PaymentIdFrom(string reference)
    {
        var parts = reference.Split('_', 2);
        return parts.Length == 2 && Guid.TryParseExact(parts[0], "N", out var id) ? id : null;
    }

    private async Task MarkResolvedAsync(Models.MobileMoneyAttempt? attempt, string? transStatus, CancellationToken ct)
    {
        if (attempt is null || attempt.ResolvedAt is not null) return;
        attempt.ResolvedAt = DateTime.UtcNow;
        attempt.Outcome = transStatus;
        await db.SaveChangesAsync(ct);
    }
}
