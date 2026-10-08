using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// The doctor declined the insurance claim — the patient needs to pay another way
public class InsuranceClaimDeclinedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email) : IConsumer<InsuranceClaimDeclinedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimDeclinedEvent> ctx)
    {
        var msg = ctx.Message;
        var body = msg.Reason is null
            ? "Le médecin n'a pas accepté votre assurance pour ce rendez-vous. Veuillez payer par carte ou Mobile Money pour le confirmer."
            : $"Le médecin n'a pas accepté votre assurance pour ce rendez-vous ({msg.Reason}). Veuillez payer par carte ou Mobile Money pour le confirmer.";

        await publisher.PublishAsync(msg.PatientId, "insurance_claim_declined", "Prise en charge refusée", body);

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.PatientId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Prise en charge refusée", $"Bonjour {contact.FirstName}, {char.ToLower(body[0])}{body[1..]}");
    }
}
