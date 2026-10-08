using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class PaymentCompletedConsumer(AppointmentDbContext db, ILogger<PaymentCompletedConsumer> log) : IConsumer<PaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null)
        {
            log.LogWarning("Payment completed for unknown appointment {AppointmentId}", context.Message.AppointmentId);
            return;
        }

        // An approved insurance claim only confirms a booking that's still waiting on it
        if (context.Message.Provider == PaymentProvider.Insurance && appt.Status != AppointmentStatus.Pending)
        {
            log.LogWarning("Insurance approval for appointment {AppointmentId} ignored: booking is already {Status}", appt.Id, appt.Status);
            return;
        }

        appt.Status = AppointmentStatus.Confirmed;
        appt.AwaitingInsuranceReview = false;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        log.LogInformation("Appointment {AppointmentId} confirmed: paid by {Provider}", appt.Id, context.Message.Provider?.ToString() ?? "payment");
    }
}
