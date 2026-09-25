using LandaDoc.Payment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Consumers;

// The booking's grace period lapsed unpaid (Appointment's expiry sweep) — close out the
// ledger the same way a failed payment webhook would, so payment history reflects why.
public class BookingExpiredConsumer(PaymentDbContext db) : IConsumer<BookingExpiredEvent>
{
    public async Task Consume(ConsumeContext<BookingExpiredEvent> ctx)
    {
        var msg = ctx.Message;
        var payment = await db.Payments
            .Where(p => p.AppointmentId == msg.AppointmentId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        // MassTransit redelivers on faults/restarts — only the still-Pending row is ours to close.
        if (payment is null || payment.Status != PaymentStatus.Pending) return;

        // Insert a new Failed row (immutable ledger — no updates); no Provider/ProviderRef,
        // since the patient never got as far as choosing one.
        db.Payments.Add(new Models.Payment
        {
            AppointmentId = payment.AppointmentId,
            DoctorId = payment.DoctorId,
            PatientId = payment.PatientId,
            GrossAmount = payment.GrossAmount,
            PlatformFee = payment.PlatformFee,
            NetAmount = payment.NetAmount,
            Status = PaymentStatus.Failed,
        });
        await db.SaveChangesAsync();
    }
}
