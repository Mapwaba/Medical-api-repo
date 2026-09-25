using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class PaymentFailedConsumer(AppointmentDbContext db) : IConsumer<PaymentFailedEvent>
{
    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null) return;

        appt.Status = AppointmentStatus.Cancelled;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
