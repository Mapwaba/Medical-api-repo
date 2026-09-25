using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// PaymentFailedEvent only carries AppointmentId/Reason — patient/doctor ids come from the
// projection row that BookingCreatedConsumer wrote when the appointment was first booked.
public class PaymentFailedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email,
    ISmsService sms) : IConsumer<PaymentFailedEvent>
{
    public async Task Consume(ConsumeContext<PaymentFailedEvent> ctx)
    {
        var msg = ctx.Message;

        var projection = await db.AppointmentPendingProjections
            .FirstOrDefaultAsync(p => p.AppointmentId == msg.AppointmentId);
        if (projection is null) return;

        // MassTransit redelivers on faults/restarts — skip if we already handled this failure
        if (projection.Status == AppointmentStatus.Cancelled) return;

        projection.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();

        await publisher.PushPendingCountAsync(projection.PatientId);
        await publisher.PushPendingCountAsync(projection.DoctorId);

        await publisher.PublishAsync(projection.PatientId, "payment_failed", "Échec du paiement",
            msg.Reason is null
                ? "Le paiement de votre rendez-vous a échoué. Le rendez-vous a été annulé."
                : $"Le paiement de votre rendez-vous a échoué ({msg.Reason}). Le rendez-vous a été annulé.");
        await publisher.PublishAsync(projection.DoctorId, "payment_failed", "Échec du paiement",
            "Le paiement d'un rendez-vous a échoué — le rendez-vous a été annulé.");

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == projection.PatientId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Échec du paiement",
            $"Bonjour {contact.FirstName}, le paiement de votre rendez-vous n'a pas pu être traité et le rendez-vous a été annulé.");

        if (!string.IsNullOrWhiteSpace(contact.Phone))
            await sms.SendAsync(contact.Phone, "LandaDoc: échec du paiement, votre rendez-vous a été annulé.");
    }
}
