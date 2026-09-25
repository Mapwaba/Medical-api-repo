using LandaDoc.Availability.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Availability.Consumers;

// Invalidates the availability cache when a booking is made, so the next
// GetSlotsAsync call recomputes instead of serving a stale cached list.
public class BookingCreatedConsumer(IAvailabilityService avail) : IConsumer<BookingCreatedEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedEvent> ctx)
    {
        var msg = ctx.Message;
        var date = DateOnly.FromDateTime(msg.SlotStart);
        await avail.InvalidateCacheAsync(msg.DoctorId, date);
    }
}
