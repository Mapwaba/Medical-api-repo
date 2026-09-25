using LandaDoc.Appointment.Data;
using LandaDoc.Appointment.Models;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class FamilyLinkAcceptedConsumer(AppointmentDbContext db) : IConsumer<FamilyLinkAcceptedEvent>
{
    public async Task Consume(ConsumeContext<FamilyLinkAcceptedEvent> context)
    {
        var msg = context.Message;

        // Idempotent — a redelivered event must not duplicate the pair of rows.
        var exists = await db.BookingAuthorizations.AnyAsync(a => a.SourceId == msg.LinkId);
        if (exists) return;

        db.BookingAuthorizations.AddRange(
            new BookingAuthorization { BookerId = msg.RequesterUserId, TargetId = msg.RecipientUserId, SourceId = msg.LinkId },
            new BookingAuthorization { BookerId = msg.RecipientUserId, TargetId = msg.RequesterUserId, SourceId = msg.LinkId });
        await db.SaveChangesAsync();
    }
}
