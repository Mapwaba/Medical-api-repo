using LandaDoc.Appointment.Data;
using LandaDoc.Appointment.Models;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

public class DependentAddedConsumer(AppointmentDbContext db) : IConsumer<DependentAddedEvent>
{
    public async Task Consume(ConsumeContext<DependentAddedEvent> context)
    {
        var msg = context.Message;

        var exists = await db.BookingAuthorizations.AnyAsync(a => a.SourceId == msg.DependentId);
        if (exists) return;

        db.BookingAuthorizations.Add(new BookingAuthorization
        {
            BookerId = msg.GuardianUserId,
            TargetId = msg.DependentId,
            IsDependentTarget = true,
            SourceId = msg.DependentId
        });
        await db.SaveChangesAsync();
    }
}
