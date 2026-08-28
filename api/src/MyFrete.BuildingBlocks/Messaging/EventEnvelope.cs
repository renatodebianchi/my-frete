using System.Text.Json;

namespace MyFrete.BuildingBlocks.Messaging;

/// <summary>
/// Marker for an event that is persisted to the outbox and (later) published to a bus.
/// The concrete type name maps to <see cref="EventEnvelope.Type"/> via <c>EventTypeAttribute</c>.
/// </summary>
public interface IIntegrationEvent
{
    string AggregateType { get; }

    Guid AggregateId { get; }
}

/// <summary>Binds a CLR event type to its wire <c>type</c> string, e.g. "matching.offer.sent.v1".</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventTypeAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}

/// <summary>
/// The transport-agnostic envelope defined in contracts/events.md. Same shape in the outbox
/// and on a future message bus.
/// </summary>
public sealed record EventEnvelope(
    Guid Id,
    string Type,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string AggregateType,
    Guid AggregateId,
    JsonElement Data)
{
    public static EventEnvelope From(IIntegrationEvent @event, string? correlationId, TimeProvider clock)
    {
        var type = @event.GetType().GetCustomAttributes(typeof(EventTypeAttribute), false)
            is [EventTypeAttribute attr, ..]
            ? attr.Type
            : throw new InvalidOperationException(
                $"Event {@event.GetType().Name} is missing [EventType(\"...\")].");

        var data = JsonSerializer.SerializeToElement(@event, @event.GetType());

        return new EventEnvelope(
            Guid.NewGuid(),
            type,
            clock.GetUtcNow(),
            correlationId,
            @event.AggregateType,
            @event.AggregateId,
            data);
    }
}
