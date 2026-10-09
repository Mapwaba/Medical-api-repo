using LandaDoc.Notification.Data;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Services;

// Who hears about the patient's side of an appointment. Usually just the patient; when a family
// member booked it for them they hear about it too, since they're the one who pays and brings
// the patient along. A dependant (a child without an account) can't receive anything, so for
// them the family member who booked is the only one really told. The booker's messages start
// with "Pour <name> :" so they know which relative it's about.
public static class PatientSide
{
    public sealed record Recipient(Guid UserId, string Prefix)
    {
        // "Votre RDV est confirmé." -> "Pour Ruth : votre RDV est confirmé." Pass lowerFirst: false
        // when the message starts with a name ("SONAS n'a pas payé...").
        public string Text(string body, bool lowerFirst = true) =>
            Prefix.Length == 0 ? body : Prefix + (lowerFirst ? char.ToLower(body[0]) + body[1..] : body);
    }

    public static async Task<List<Recipient>> RecipientsAsync(NotificationDbContext db, Guid appointmentId, Guid patientId)
    {
        var projection = await db.AppointmentPendingProjections.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId);
        var recipients = new List<Recipient> { new(patientId, "") };
        if (projection?.BookedByUserId is { } booker && booker != patientId)
            recipients.Add(new(booker, $"Pour {projection.PatientName ?? "votre proche"} : "));
        return recipients;
    }
}
