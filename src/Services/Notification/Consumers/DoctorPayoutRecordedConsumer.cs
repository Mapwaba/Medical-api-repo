using System.Globalization;
using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// An admin recorded money sent to a doctor — tell the doctor where it went
public class DoctorPayoutRecordedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email) : IConsumer<DoctorPayoutRecordedEvent>
{
    public async Task Consume(ConsumeContext<DoctorPayoutRecordedEvent> ctx)
    {
        var msg = ctx.Message;
        var amount = msg.Amount.ToString("0.00", CultureInfo.InvariantCulture) + " $";
        var reference = msg.Reference is null ? "" : $" (réf. {msg.Reference})";
        var body = $"LandaDoc vous a versé {amount} sur {msg.PaidTo}{reference}. Le détail est dans Paiements.";

        await publisher.PublishAsync(msg.DoctorId, "doctor_payout", "Versement effectué", body);

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.DoctorId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Versement effectué", $"Bonjour {contact.FirstName}, {char.ToLower(body[0])}{body[1..]}");
    }
}
