using System.Globalization;
using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// The insurer refused to pay a claim the doctor had accepted — the appointment stands, but the
// patient now owes the doctor the amount directly
public class InsuranceClaimRejectedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email) : IConsumer<InsuranceClaimRejectedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimRejectedEvent> ctx)
    {
        var msg = ctx.Message;
        var amount = msg.Amount.ToString("0.00", CultureInfo.InvariantCulture) + " $";
        var reason = msg.Reason is null ? "" : $" ({msg.Reason})";
        var body = $"{msg.InsurerName} n'a pas payé votre rendez-vous{reason}. Votre rendez-vous reste valable, " +
                   $"mais le montant de {amount} est maintenant à régler directement auprès du médecin.";

        foreach (var r in await PatientSide.RecipientsAsync(db, msg.AppointmentId, msg.PatientId))
        {
            var text = r.Text(body, lowerFirst: false);   // starts with the insurer's name
            await publisher.PublishAsync(r.UserId, "insurance_claim_rejected", "Assurance : paiement refusé", text);

            var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == r.UserId);
            if (contact is null) continue;
            await email.SendAsync(contact.Email, "Assurance : paiement refusé", $"Bonjour {contact.FirstName}, {text}");
        }
    }
}
