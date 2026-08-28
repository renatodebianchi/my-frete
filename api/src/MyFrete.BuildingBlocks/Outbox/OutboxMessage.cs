namespace MyFrete.BuildingBlocks.Outbox;

public enum OutboxState
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

/// <summary>Transactional outbox row (data-model.md §Notifications / Cross-cutting).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Type { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? CorrelationId { get; init; }

    public required string AggregateType { get; init; }

    public Guid AggregateId { get; init; }

    /// <summary>The full <c>EventEnvelope</c> serialized as JSON.</summary>
    public required string Payload { get; init; }

    /// <summary>Optional dedupe key: <c>{type}:{recipientId}:{aggregateId}</c>.</summary>
    public string? DedupeKey { get; init; }

    public OutboxState State { get; set; } = OutboxState.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? SentAt { get; set; }
}
