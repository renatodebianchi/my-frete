using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.Modules.Trips.Contracts;

/// <summary>Matching / Scheduling call this synchronously when an offer is accepted.</summary>
public interface ITripFactory
{
    Task<Guid> CreateAsync(
        Guid requestId,
        Guid clientId,
        Guid professionalId,
        decimal agreedAmount,
        string currency,
        CancellationToken ct = default);
}

[EventType("trip.created.v1")]
public sealed record TripCreated(Guid AggregateId, Guid RequestId, Guid ClientId, Guid ProfessionalId, decimal AgreedAmount)
    : IIntegrationEvent
{
    public string AggregateType => "Trip";
}

[EventType("trip.delivered.v1")]
public sealed record TripDelivered(Guid AggregateId, Guid RequestId, Guid ClientId, Guid ProfessionalId)
    : IIntegrationEvent
{
    public string AggregateType => "Trip";
}

[EventType("trip.client_responded.v1")]
public sealed record TripClientResponded(Guid AggregateId, Guid ClientId, Guid ProfessionalId, string Response)
    : IIntegrationEvent
{
    public string AggregateType => "Trip";
}

[EventType("trip.verification_due.v1")]
public sealed record TripVerificationDue(Guid AggregateId, Guid ClientId, Guid ProfessionalId)
    : IIntegrationEvent
{
    public string AggregateType => "Trip";
}

[EventType("trip.cancelled.v1")]
public sealed record TripCancelled(Guid AggregateId, Guid RequestId, string By)
    : IIntegrationEvent
{
    public string AggregateType => "Trip";
}
