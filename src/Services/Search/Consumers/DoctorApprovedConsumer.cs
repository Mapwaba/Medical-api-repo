using LandaDoc.Search.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Search.Consumers;

public class DoctorApprovedConsumer(ISearchIndexService index) : IConsumer<DoctorApprovedEvent>
{
    public Task Consume(ConsumeContext<DoctorApprovedEvent> ctx) => index.UpsertDoctorAsync(ctx.Message);
}
