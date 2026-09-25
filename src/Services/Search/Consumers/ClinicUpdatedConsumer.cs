using LandaDoc.Search.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Search.Consumers;

public class ClinicUpdatedConsumer(ISearchIndexService index) : IConsumer<ClinicUpdatedEvent>
{
    public Task Consume(ConsumeContext<ClinicUpdatedEvent> ctx) => index.UpdateClinicAsync(ctx.Message);
}
