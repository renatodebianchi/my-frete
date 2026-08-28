namespace MyFrete.BuildingBlocks.Audit;

/// <summary>Who is acting in the current scope, for audit and outbox correlation.</summary>
public interface ICurrentActor
{
    /// <summary><c>user:&lt;id&gt;</c> | <c>system</c> | <c>operator:&lt;id&gt;</c>.</summary>
    string Actor { get; }

    Guid? UserId { get; }

    string? CorrelationId { get; }
}

/// <summary>Fallback used by background workers and design-time.</summary>
public sealed class SystemActor : ICurrentActor
{
    public string Actor => "system";

    public Guid? UserId => null;

    public string? CorrelationId => null;
}
