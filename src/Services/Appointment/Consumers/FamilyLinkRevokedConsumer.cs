using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class FamilyLinkRevokedConsumer(AppointmentDbContext db) : IConsumer<FamilyLinkRevokedEvent>
{
    public async Task Consume(ConsumeContext<FamilyLinkRevokedEvent> context)
    {
        await db.BookingAuthorizations
            .Where(a => a.SourceId == context.Message.LinkId)
            .ExecuteDeleteAsync();
    }
}
