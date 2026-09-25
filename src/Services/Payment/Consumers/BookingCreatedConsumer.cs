using LandaDoc.Payment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Consumers;

// Reacts to BookingCreated — creates the payment ledger entry
public class BookingCreatedConsumer(PaymentDbContext db) : IConsumer<BookingCreatedEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedEvent> ctx)
    {
        var msg = ctx.Message;

        // MassTransit redelivers on faults/restarts — guard the insert-only ledger against duplicates
        var alreadyExists = await db.Payments
            .AnyAsync(p => p.AppointmentId == msg.AppointmentId);
        if (alreadyExists) return;

        // Payment doesn't own doctor profile data — pricing comes from the local
        // read-model built by DoctorApprovedConsumer. No fee synced yet? Nothing to charge.
        var fee = await db.DoctorFees.FirstOrDefaultAsync(f => f.DoctorId == msg.DoctorId);
        if (fee is null) return;

        var gross = fee.ConsultationFee;
        var platformFee = Math.Round(gross * fee.PlatformFeePct / 100, 2);
        var net = gross - platformFee;

        // Immutable ledger — insert only, never update
        var payment = new Models.Payment
        {
            AppointmentId = msg.AppointmentId,
            DoctorId = msg.DoctorId,
            PatientId = msg.PatientId,
            GrossAmount = gross,
            PlatformFee = platformFee,
            NetAmount = net,
            Status = PaymentStatus.Pending,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        // Patient then calls POST /api/payments/{id}/initiate to trigger the Stripe checkout
    }
}
