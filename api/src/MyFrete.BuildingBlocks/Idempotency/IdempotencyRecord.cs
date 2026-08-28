namespace MyFrete.BuildingBlocks.Idempotency;

/// <summary>Stored result of a previously-processed request keyed by the <c>Idempotency-Key</c> header.</summary>
public sealed class IdempotencyRecord
{
    public required string Key { get; init; }

    /// <summary>Hash of method + path + body; a replay with a different hash is a client error.</summary>
    public required string RequestHash { get; init; }

    public int ResponseStatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public string? ResponseContentType { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
