using LandaDoc.Availability.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Availability.Consumers;

// Invalidates the availability cache for both the freed-up old date and the
// newly-taken date, so the next GetSlotsAsync call for either recomputes.
public class AppointmentRescheduledConsumer(IAvailabilityService avail) : IConsumer<AppointmentRescheduledEvent>
{
    public async Task Consume(ConsumeContext<AppointmentRescheduledEvent> ctx)
    {
        var msg = ctx.Message;
        await avail.InvalidateCacheAsync(msg.DoctorId, DateOnly.FromDateTime(msg.OldSlotStart));
        await avail.InvalidateCacheAsync(msg.DoctorId, DateOnly.FromDateTime(msg.NewSlotStart));
    }
}
