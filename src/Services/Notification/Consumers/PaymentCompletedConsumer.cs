using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// SMS/Email live here, not on booking — a receipt only makes sense once payment succeeds.
public class PaymentCompletedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email,
    ISmsService sms) : IConsumer<PaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> ctx)
    {
        var msg = ctx.Message;

        var projection = await db.AppointmentPendingProjections
            .FirstOrDefaultAsync(p => p.AppointmentId == msg.AppointmentId);
        if (projection is null) return;

        // MassTransit redelivers on faults/restarts — skip if we already confirmed this one
        if (projection.Status == AppointmentStatus.Confirmed) return;

        projection.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync();

        await publisher.PushPendingCountAsync(msg.PatientId);
        await publisher.PushPendingCountAsync(msg.DoctorId);

        await publisher.PublishAsync(msg.PatientId, "payment_completed", "Paiement reçu",
            "Votre paiement a été reçu et votre rendez-vous est confirmé.");
        await publisher.PublishAsync(msg.DoctorId, "payment_completed", "Paiement reçu",
            "Le paiement d'un rendez-vous a été reçu — le rendez-vous est confirmé.");

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.PatientId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Paiement reçu",
            $"Bonjour {contact.FirstName}, votre paiement pour le rendez-vous du {msg.OccurredAt:dd/MM/yyyy} a bien été reçu. Votre rendez-vous est confirmé.");

        if (!string.IsNullOrWhiteSpace(contact.Phone))
            await sms.SendAsync(contact.Phone, "LandaDoc: paiement reçu, votre rendez-vous est confirmé.");
    }
}
