using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Contracts;
using MyFrete.Modules.Accounts.Domain;
using NetTopologySuite.Geometries;

namespace MyFrete.Modules.Accounts.Professionals;

public sealed class NoActiveTripGuard : IActiveTripGuard
{
    public Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<IReadOnlySet<Guid>> WithActiveTripAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}

public sealed record EligibleProfessional(Guid ProfessionalId, double? DistanceMeters, bool LocationStale);

/// <summary>
/// Read model over professional capacity/availability/proximity. Matching (US1) consumes this
/// for the immediate-offer order (FR-011, FR-012, FR-012a).
/// </summary>
public interface IProfessionalDirectory
{
    Task<IReadOnlyList<Guid>> GetEligibleForImmediateAsync(int weightGrams, CancellationToken ct = default);

    Task<IReadOnlyList<EligibleProfessional>> GetEligibleOrderedByProximityAsync(
        int weightGrams,
        double originLat,
        double originLng,
        TimeSpan locationTtl,
        double? radiusMeters,
        CancellationToken ct = default);
}

public sealed class ProfessionalDirectory(DbContext db, IActiveTripGuard activeTrips, TimeProvider clock)
    : IProfessionalDirectory
{
    private static readonly GeometryFactory Geo =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public async Task<IReadOnlyList<Guid>> GetEligibleForImmediateAsync(int weightGrams, CancellationToken ct = default)
    {
        var candidates = await db.Set<ProfessionalProfile>()
            .Where(p => p.ImmediateAvailability && p.MaxLoadGrams >= weightGrams)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return await ExcludeBusyAsync(candidates, ct);
    }

    public async Task<IReadOnlyList<EligibleProfessional>> GetEligibleOrderedByProximityAsync(
        int weightGrams,
        double originLat,
        double originLng,
        TimeSpan locationTtl,
        double? radiusMeters,
        CancellationToken ct = default)
    {
        var origin = Geo.CreatePoint(new Coordinate(originLng, originLat));
        var staleBefore = clock.GetUtcNow() - locationTtl;

        var query = db.Set<ProfessionalProfile>()
            .Where(p => p.ImmediateAvailability && p.MaxLoadGrams >= weightGrams);

        if (radiusMeters is { } radius)
        {
            query = query.Where(p => p.LastLocation == null || p.LastLocation.Distance(origin) <= radius);
        }

        var rows = await query
            .Select(p => new
            {
                p.UserId,
                Distance = p.LastLocation == null ? (double?)null : p.LastLocation.Distance(origin),
                Stale = p.LastLocationAt == null || p.LastLocationAt < staleBefore,
            })
            .ToListAsync(ct);

        var busy = await activeTrips.WithActiveTripAsync(rows.Select(r => r.UserId).ToList(), ct);

        return rows
            .Where(r => !busy.Contains(r.UserId))
            .OrderBy(r => r.Stale)                       // fresh-location professionals first
            .ThenBy(r => r.Distance ?? double.MaxValue)  // then nearest
            .Select(r => new EligibleProfessional(r.UserId, r.Distance, r.Stale))
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> ExcludeBusyAsync(List<Guid> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var busy = await activeTrips.WithActiveTripAsync(candidates, ct);
        return busy.Count == 0 ? candidates : candidates.Where(id => !busy.Contains(id)).ToList();
    }
}
