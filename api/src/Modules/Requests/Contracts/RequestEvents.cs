using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.Modules.Requests.Contracts;

[EventType("request.confirmed.v1")]
public sealed record RequestConfirmed(
    Guid AggregateId,
    Guid ClientId,
    double OriginLat,
    double OriginLng,
    double DestinationLat,
    double DestinationLng,
    int WeightGrams) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}

[EventType("request.status_changed.v1")]
public sealed record RequestStatusChanged(Guid AggregateId, string From, string To) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}

[EventType("request.cancelled.v1")]
public sealed record RequestCancelled(Guid AggregateId, string By) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}

[EventType("request.schedule_requested.v1")]
public sealed record RequestScheduleRequested(
    Guid AggregateId,
    Guid ClientId,
    DateOnly ScheduledDate,
    int WeightGrams,
    decimal EstimatedPrice,
    string Currency) : IIntegrationEvent
{
    public string AggregateType => "TransportRequest";
}

/// <summary>Cross-module contract: another module tells Requests a professional was assigned.</summary>
public interface IRequestAssignment
{
    Task<bool> TryAssignAsync(Guid requestId, Guid professionalId, CancellationToken ct = default);

    Task MarkExhaustedAsync(Guid requestId, CancellationToken ct = default);

    Task MarkCompletedAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>A professional cancelled after accepting — put the request back into the search (FR-027).</summary>
    Task ReopenAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>No professional accepted the scheduled request by its date (FR-024).</summary>
    Task MarkUnfulfilledScheduledAsync(Guid requestId, CancellationToken ct = default);
}
