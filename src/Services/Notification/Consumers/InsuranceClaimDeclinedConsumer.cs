using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// The doctor declined the insurance claim, or didn't answer in time — the patient needs to pay another way
public class InsuranceClaimDeclinedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email) : IConsumer<InsuranceClaimDeclinedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimDeclinedEvent> ctx)
    {
        var msg = ctx.Message;
        var body = msg.TimedOut
            ? "Le médecin n'a pas pu vérifier votre assurance à temps. Veuillez payer par carte ou Mobile Money pour confirmer votre rendez-vous."
            : msg.Reason is null
                ? "Le médecin n'a pas accepté votre assurance pour ce rendez-vous. Veuillez payer par carte ou Mobile Money pour le confirmer."
                : $"Le médecin n'a pas accepté votre assurance pour ce rendez-vous ({msg.Reason}). Veuillez payer par carte ou Mobile Money pour le confirmer.";

        var patientSide = await PatientSide.RecipientsAsync(db, msg.AppointmentId, msg.PatientId);
        foreach (var r in patientSide)
            await publisher.PublishAsync(r.UserId, "insurance_claim_declined", "Prise en charge refusée", r.Text(body));

        // Let the doctor know the claim they left waiting has been closed
        if (msg.TimedOut)
            await publisher.PublishAsync(msg.DoctorId, "insurance_claim_expired", "Prise en charge expirée",
                "Une demande de prise en charge a été refusée automatiquement faute de réponse. Le patient a été invité à payer autrement.");

        foreach (var r in patientSide)
        {
            var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == r.UserId);
            if (contact is null) continue;
            var text = r.Text(body);
            await email.SendAsync(contact.Email, "Prise en charge refusée", $"Bonjour {contact.FirstName}, {char.ToLower(text[0])}{text[1..]}");
        }
    }
}
