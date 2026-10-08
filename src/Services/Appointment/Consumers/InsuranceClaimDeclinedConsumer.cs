using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

// The doctor declined the insurance claim — the booking is unpaid again. The patient gets
// Booking:DeclinedClaimPayHours (default 24h, but never past the slot itself) to pay another
// way before the usual expiry sweep cancels it.
public class InsuranceClaimDeclinedConsumer(AppointmentDbContext db, IConfiguration cfg) : IConsumer<InsuranceClaimDeclinedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimDeclinedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null || appt.Status != AppointmentStatus.Pending || !appt.AwaitingInsuranceReview) return;

        var hours = cfg.GetValue("Booking:DeclinedClaimPayHours", 24);
        var dueAt = DateTime.UtcNow.AddHours(hours);
        appt.AwaitingInsuranceReview = false;
        appt.PaymentDueAt = dueAt < appt.SlotStart ? dueAt : appt.SlotStart;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
