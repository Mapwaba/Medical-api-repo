using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class DependentRemovedConsumer(AppointmentDbContext db) : IConsumer<DependentRemovedEvent>
{
    public async Task Consume(ConsumeContext<DependentRemovedEvent> context)
    {
        await db.BookingAuthorizations
            .Where(a => a.SourceId == context.Message.DependentId)
            .ExecuteDeleteAsync();
    }
}
