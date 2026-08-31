using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.Modules.Scheduling.Contracts;

[EventType("scheduling.broadcast.sent.v1")]
public sealed record SchedulingBroadcastSent(Guid AggregateId, DateOnly ScheduledDate, int ProfessionalCount)
    : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}

[EventType("scheduling.offer.sent.v1")]
public sealed record ScheduledOfferSent(Guid AggregateId, Guid RequestId, Guid ProfessionalId, DateOnly ScheduledDate)
    : IIntegrationEvent
{
    public string AggregateType => "ScheduledOffer";
}

[EventType("scheduling.offer.accepted.v1")]
public sealed record ScheduledOfferAccepted(Guid AggregateId, Guid RequestId, Guid ClientId, Guid ProfessionalId, Guid TripId)
    : IIntegrationEvent
{
    public string AggregateType => "ScheduledOffer";
}

[EventType("scheduling.offer.filled_by_other.v1")]
public sealed record ScheduledOfferFilledByOther(Guid AggregateId, Guid ProfessionalId) : IIntegrationEvent
{
    public string AggregateType => "ScheduledOffer";
}

[EventType("scheduling.unfulfilled.v1")]
public sealed record SchedulingUnfulfilled(Guid AggregateId, Guid ClientId, string Reason) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}
