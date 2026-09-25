using LandaDoc.Payment.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Consumers;

// Builds Payment's local read-model of "what does this doctor charge" —
// Payment doesn't own doctor profile data, only enough to price a booking.
public class DoctorApprovedConsumer(PaymentDbContext db) : IConsumer<DoctorApprovedEvent>
{
    public async Task Consume(ConsumeContext<DoctorApprovedEvent> ctx)
    {
        var msg = ctx.Message;
        var fee = await db.DoctorFees.FirstOrDefaultAsync(f => f.DoctorId == msg.UserId);
        if (fee is null)
        {
            db.DoctorFees.Add(new Models.DoctorFee
            {
                DoctorId = msg.UserId,
                ConsultationFee = msg.ConsultationFee,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            fee.ConsultationFee = msg.ConsultationFee;
            fee.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
