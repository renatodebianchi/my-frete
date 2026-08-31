using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.BuildingBlocks.Outbox;

/// <summary>
/// Enqueues an integration event into the outbox in the *same* transaction as the state change
/// (Constitution §VI). Does not call SaveChanges — the caller's unit of work does.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(IIntegrationEvent @event, string? dedupeKey = null);
}

public sealed class OutboxWriter(DbContext db, ICurrentActor actor, TimeProvider clock) : IOutboxWriter
{
    public void Enqueue(IIntegrationEvent @event, string? dedupeKey = null)
    {
        var envelope = EventEnvelope.From(@event, actor.CorrelationId, clock);
        var now = clock.GetUtcNow();

        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = envelope.Id,
            Type = envelope.Type,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            AggregateType = envelope.AggregateType,
            AggregateId = envelope.AggregateId,
            Payload = JsonSerializer.Serialize(envelope, MyFreteJson.Options),
            DedupeKey = dedupeKey,
            State = OutboxState.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }
}
