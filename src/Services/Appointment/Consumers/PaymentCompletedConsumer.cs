using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class PaymentCompletedConsumer(AppointmentDbContext db) : IConsumer<PaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null) return;

        appt.Status = AppointmentStatus.Confirmed;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
