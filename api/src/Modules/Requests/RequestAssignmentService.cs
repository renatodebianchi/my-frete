using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests;

/// <summary>
/// Lets Matching / Scheduling drive the request lifecycle without touching the table directly.
/// Callers run inside the pipeline's unit of work, so no SaveChanges here.
/// </summary>
public sealed class RequestAssignmentService(DbContext db, IOutboxWriter outbox, TimeProvider clock) : IRequestAssignment
{
    public async Task<bool> TryAssignAsync(Guid requestId, Guid professionalId, CancellationToken ct = default)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null || r.Status is not (RequestStatus.Searching or RequestStatus.ScheduledSearching))
        {
            return false;
        }

        var from = r.Status.ToWire();
        r.Assign(professionalId, clock.GetUtcNow());
        outbox.Enqueue(new RequestStatusChanged(r.Id, from, r.Status.ToWire()));
        return true;
    }

    public async Task MarkExhaustedAsync(Guid requestId, CancellationToken ct = default)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null || r.Status != RequestStatus.Searching)
        {
            return;
        }

        r.MarkExhausted(clock.GetUtcNow());
        outbox.Enqueue(new RequestStatusChanged(r.Id, "searching", "awaiting_schedule_decision"));
    }

    public async Task MarkCompletedAsync(Guid requestId, CancellationToken ct = default)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null || r.Status is RequestStatus.Completed or RequestStatus.Cancelled)
        {
            return;
        }

        var from = r.Status.ToWire();
        r.MarkCompleted(clock.GetUtcNow());
        outbox.Enqueue(new RequestStatusChanged(r.Id, from, "completed"));
    }
}
