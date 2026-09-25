using LandaDoc.Search.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Search.Consumers;

public class DoctorSuspendedConsumer(ISearchIndexService index) : IConsumer<DoctorSuspendedEvent>
{
    public Task Consume(ConsumeContext<DoctorSuspendedEvent> ctx) => index.RemoveDoctorAsync(ctx.Message.UserId);
}
