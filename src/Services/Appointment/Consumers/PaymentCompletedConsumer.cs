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

        // An approved insurance claim only confirms a booking that's still waiting on it
        if (context.Message.Provider == PaymentProvider.Insurance && appt.Status != AppointmentStatus.Pending) return;

        appt.Status = AppointmentStatus.Confirmed;
        appt.AwaitingInsuranceReview = false;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
