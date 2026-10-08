using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// An insurance claim has waited half its review time — remind the doctor before it's
// declined automatically
public class InsuranceReviewReminderConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IEmailService email) : IConsumer<InsuranceReviewReminderEvent>
{
    public async Task Consume(ConsumeContext<InsuranceReviewReminderEvent> ctx)
    {
        var msg = ctx.Message;
        // The deadline is real UTC and the server doesn't know the doctor's time zone, so say
        // how long is left rather than a clock time
        var hoursLeft = Math.Max(1, (int)Math.Round((msg.DueAt - DateTime.UtcNow).TotalHours));
        var body = $"Une demande de prise en charge attend votre réponse. Sans réponse d'ici {hoursLeft} h, " +
                   "elle sera refusée automatiquement et le patient devra payer autrement. Répondez dans Paiements.";

        await publisher.PublishAsync(msg.DoctorId, "insurance_review_reminder", "Prise en charge en attente", body);

        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.DoctorId);
        if (contact is null) return;

        await email.SendAsync(contact.Email, "Prise en charge en attente", $"Bonjour {contact.FirstName}, {char.ToLower(body[0])}{body[1..]}");
    }
}
