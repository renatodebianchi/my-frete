namespace MyFrete.BuildingBlocks.Contracts;

/// <summary>
/// True while a professional has an accepted, not-yet-completed transport. Accounts ships a
/// no-op default; the Trips module replaces it with the real query (FR-011, FR-011a).
/// </summary>
public interface IActiveTripGuard
{
    Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default);

    Task<IReadOnlySet<Guid>> WithActiveTripAsync(
        IReadOnlyCollection<Guid> professionalIds,
        CancellationToken ct = default);
}
