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

        var patientSide = await PatientSide.RecipientsAsync(db, projection.AppointmentId, projection.PatientId);
        foreach (var r in patientSide) await publisher.PushPendingCountAsync(r.UserId);
        await publisher.PushPendingCountAsync(projection.DoctorId);

        await publisher.PublishAsync(projection.DoctorId, "booking_expired", "Réservation expirée",
            "Un rendez-vous a été annulé faute de paiement dans les délais.");
        foreach (var r in patientSide)
        {
            await publisher.PublishAsync(r.UserId, "booking_expired", "Réservation expirée",
                r.Text("Le paiement n'a pas été effectué à temps. Le rendez-vous a été annulé."));

            var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == r.UserId);
            if (contact is null) continue;

            var text = r.Text("Le paiement de votre rendez-vous n'a pas été effectué à temps et le rendez-vous a été annulé.");
            await email.SendAsync(contact.Email, "Réservation expirée", $"Bonjour {contact.FirstName}, {char.ToLower(text[0])}{text[1..]}");

            if (!string.IsNullOrWhiteSpace(contact.Phone))
                await sms.SendAsync(contact.Phone, "LandaDoc: paiement non reçu à temps, le rendez-vous a été annulé.");
        }
    }
}
