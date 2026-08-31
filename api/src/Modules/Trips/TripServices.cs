using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Contracts;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Trips.Contracts;
using MyFrete.Modules.Trips.Domain;

namespace MyFrete.Modules.Trips;

public sealed class TripFactory(DbContext db, IOutboxWriter outbox, TimeProvider clock) : ITripFactory
{
    public async Task<Guid> CreateAsync(
        Guid requestId,
        Guid clientId,
        Guid professionalId,
        decimal agreedAmount,
        string currency,
        CancellationToken ct = default)
    {
        var existing = await db.Set<Trip>().FirstOrDefaultAsync(t => t.RequestId == requestId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var trip = Trip.Create(requestId, clientId, professionalId, agreedAmount, currency, clock.GetUtcNow());
        db.Set<Trip>().Add(trip);
        outbox.Enqueue(new TripCreated(trip.Id, requestId, clientId, professionalId, agreedAmount));
        return trip.Id;
    }
}

/// <summary>Real implementation of the availability guard: a professional is busy while a trip is active.</summary>
public sealed class TripActiveTripGuard(DbContext db) : IActiveTripGuard
{
    private static readonly TripStatus[] Active = [TripStatus.Contratada, TripStatus.EmAndamento];

    public Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default) =>
        db.Set<Trip>().AnyAsync(t => t.ProfessionalId == professionalId && Active.Contains(t.Status), ct);

    public async Task<IReadOnlySet<Guid>> WithActiveTripAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default)
    {
        if (professionalIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var busy = await db.Set<Trip>()
            .Where(t => professionalIds.Contains(t.ProfessionalId) && Active.Contains(t.Status))
            .Select(t => t.ProfessionalId)
            .Distinct()
            .ToListAsync(ct);

        return busy.ToHashSet();
    }
}
