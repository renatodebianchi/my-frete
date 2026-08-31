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
        var meta = await db.Set<TransportRequest>()
            .Where(x => x.Id == requestId)
            .Select(x => new { x.Kind, x.Status })
            .FirstOrDefaultAsync(ct);

        if (meta is null || meta.Status is not (RequestStatus.Searching or RequestStatus.ScheduledSearching))
        {
            return false;
        }

        var target = meta.Kind == RequestKind.Scheduled ? RequestStatus.Scheduled : RequestStatus.Hired;
        var now = clock.GetUtcNow();

        // Atomic claim: only one concurrent accepter changes a row still in a search state.
        var affected = await db.Set<TransportRequest>()
            .Where(x => x.Id == requestId
                && (x.Status == RequestStatus.Searching || x.Status == RequestStatus.ScheduledSearching)
                && x.AssignedProfessionalId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, target)
                .SetProperty(x => x.AssignedProfessionalId, professionalId)
                .SetProperty(x => x.UpdatedAt, now), ct);

        if (affected != 1)
        {
            return false;
        }

        outbox.Enqueue(new RequestStatusChanged(requestId, meta.Status.ToWire(), target.ToWire()));
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

    public async Task ReopenAsync(Guid requestId, CancellationToken ct = default)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null || r.Status is RequestStatus.Cancelled or RequestStatus.Completed)
        {
            return;
        }

        var from = r.Status.ToWire();
        r.Reopen(clock.GetUtcNow());
        outbox.Enqueue(new RequestStatusChanged(r.Id, from, r.Status.ToWire()));
    }

    public async Task MarkUnfulfilledScheduledAsync(Guid requestId, CancellationToken ct = default)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null || r.Status != RequestStatus.ScheduledSearching)
        {
            return;
        }

        r.MarkUnfulfilled(clock.GetUtcNow());
        outbox.Enqueue(new RequestStatusChanged(r.Id, "scheduled_searching", "unfulfilled"));
    }
}
