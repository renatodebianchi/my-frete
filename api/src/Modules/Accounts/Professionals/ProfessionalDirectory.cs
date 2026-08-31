using Microsoft.EntityFrameworkCore;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Professionals;

/// <summary>
/// True while the professional has an accepted, not-yet-completed transport. Accounts owns the
/// availability state; the Trips module replaces this no-op implementation in US1 (FR-011a).
/// </summary>
public interface IActiveTripGuard
{
    Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> WithActiveTripAsync(IReadOnlyCollection<Guid> professionalIds, CancellationToken ct = default);
}

public sealed class NoActiveTripGuard : IActiveTripGuard
{
    public Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<IReadOnlySet<Guid>> WithActiveTripAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}

/// <summary>
/// Read model over professional capacity/availability. Matching (US1) combines this with
/// proximity ordering (FR-011, FR-012).
/// </summary>
public interface IProfessionalDirectory
{
    Task<IReadOnlyList<Guid>> GetEligibleForImmediateAsync(int weightGrams, CancellationToken ct = default);
}

public sealed class ProfessionalDirectory(DbContext db, IActiveTripGuard activeTrips) : IProfessionalDirectory
{
    public async Task<IReadOnlyList<Guid>> GetEligibleForImmediateAsync(int weightGrams, CancellationToken ct = default)
    {
        var candidates = await db.Set<ProfessionalProfile>()
            .Where(p => p.ImmediateAvailability && p.MaxLoadGrams >= weightGrams)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return candidates;
        }

        var busy = await activeTrips.WithActiveTripAsync(candidates, ct);
        return busy.Count == 0 ? candidates : candidates.Where(id => !busy.Contains(id)).ToList();
    }
}
