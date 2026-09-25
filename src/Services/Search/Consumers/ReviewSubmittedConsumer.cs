using LandaDoc.Search.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Search.Consumers;

public class ReviewSubmittedConsumer(ISearchIndexService index) : IConsumer<ReviewSubmittedEvent>
{
    public Task Consume(ConsumeContext<ReviewSubmittedEvent> ctx) =>
        index.UpdateRatingAsync(ctx.Message.DoctorId, ctx.Message.ReviewId, ctx.Message.Rating);
}
