namespace MyFrete.BuildingBlocks.Audit;

/// <summary>Append-only audit record (FR-032, Constitution §Security).</summary>
public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary><c>user:&lt;id&gt;</c> | <c>system</c> | <c>operator:&lt;id&gt;</c>.</summary>
    public required string Actor { get; init; }

    /// <summary>e.g. <c>offer.accepted</c>, <c>request.cancelled</c>, <c>verification.changed</c>.</summary>
    public required string Action { get; init; }

    public required string AggregateType { get; init; }

    public Guid AggregateId { get; init; }

    /// <summary>JSON metadata; MUST NOT contain unnecessary sensitive data.</summary>
    public string? Metadata { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAuditLog
{
    Task WriteAsync(
        string action,
        string aggregateType,
        Guid aggregateId,
        object? metadata = null,
        string? actorOverride = null,
        CancellationToken ct = default);
}
