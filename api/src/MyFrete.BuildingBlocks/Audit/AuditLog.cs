using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MyFrete.BuildingBlocks.Audit;

public sealed class AuditLog(DbContext db, ICurrentActor currentActor, TimeProvider clock) : IAuditLog
{
    public async Task WriteAsync(
        string action,
        string aggregateType,
        Guid aggregateId,
        object? metadata = null,
        string? actorOverride = null,
        CancellationToken ct = default)
    {
        db.Set<AuditEvent>().Add(new AuditEvent
        {
            Actor = actorOverride ?? currentActor.Actor,
            Action = action,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
            OccurredAt = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
    }
}
