using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.Modules.Matching.Contracts;

[EventType("matching.offer.sent.v1")]
public sealed record OfferSent(Guid AggregateId, Guid RequestId, Guid ProfessionalId, DateTimeOffset RespondBy)
    : IIntegrationEvent
{
    public string AggregateType => "Offer";
}

[EventType("matching.offer.accepted.v1")]
public sealed record OfferAccepted(Guid AggregateId, Guid RequestId, Guid ClientId, Guid ProfessionalId, Guid TripId)
    : IIntegrationEvent
{
    public string AggregateType => "Offer";
}

[EventType("matching.offer.declined.v1")]
public sealed record OfferDeclined(Guid AggregateId, Guid ProfessionalId) : IIntegrationEvent
{
    public string AggregateType => "Offer";
}

[EventType("matching.offer.expired.v1")]
public sealed record OfferExpired(Guid AggregateId, Guid ProfessionalId) : IIntegrationEvent
{
    public string AggregateType => "Offer";
}

[EventType("matching.exhausted.v1")]
public sealed record MatchingExhausted(Guid AggregateId, Guid ClientId, string Reason) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}
