using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// BookingExpiredEvent only carries AppointmentId/DoctorId/PatientId — same shape as
// PaymentFailedConsumer's handling, since an expiry is just another way a booking ends
// up unpaid.
public class BookingExpiredConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email,
    ISmsService sms) : IConsumer<BookingExpiredEvent>
{
    public async Task Consume(ConsumeContext<BookingExpiredEvent> ctx)
    {
        var msg = ctx.Message;

        var projection = await db.AppointmentPendingProjections
            .FirstOrDefaultAsync(p => p.AppointmentId == msg.AppointmentId);
        if (projection is null) return;

        // MassTransit redelivers on faults/restarts — skip if we already handled this expiry
        if (projection.Status == AppointmentStatus.Cancelled) return;

        projection.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();

        await publisher.PushPendingCountAsync(projection.PatientId);
        await publisher.PushPendingCountAsync(projection.DoctorId);

        await publisher.PublishAsync(projection.PatientId, "booking_expired", "Réservation expirée",
            "Le paiement n'a pas été effectué à temps. Le rendez-vous a été annulé.");
        await publisher.PublishAsync(projection.DoctorId, "booking_expired", "Réservation expirée",
            "Un rendez-vous a été annulé faute de paiement dans les délais.");

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == projection.PatientId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Réservation expirée",
            $"Bonjour {contact.FirstName}, le paiement de votre rendez-vous n'a pas été effectué à temps et le rendez-vous a été annulé.");

        if (!string.IsNullOrWhiteSpace(contact.Phone))
            await sms.SendAsync(contact.Phone, "LandaDoc: paiement non reçu à temps, votre rendez-vous a été annulé.");
    }
}
