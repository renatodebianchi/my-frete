using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Pricing.Domain;
using MyFrete.Modules.Pricing.Routing;

namespace MyFrete.Modules.Pricing;

public sealed record PriceEstimate(
    decimal Amount,
    string Currency,
    double DistanceMeters,
    string DistanceSource,
    Guid PricingRuleId)
{
    public double DistanceKm => Math.Round(DistanceMeters / 1000.0, 2);
}

/// <summary>
/// Public contract other modules use to price a trip. Requests snapshots the returned
/// <see cref="PriceEstimate.PricingRuleId"/> onto the request.
/// </summary>
public interface IPricingService
{
    Task<Result<PriceEstimate>> EstimateAsync(
        GeoPoint origin,
        GeoPoint destination,
        int weightGrams,
        CancellationToken ct = default);
}

public sealed class PricingService(DbContext db, IRouteDistanceProvider routing, TimeProvider clock) : IPricingService
{
    public async Task<Result<PriceEstimate>> EstimateAsync(
        GeoPoint origin,
        GeoPoint destination,
        int weightGrams,
        CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var rule = await db.Set<PricingRule>()
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        if (rule is null)
        {
            return Error.Failure("pricing.no_active_rule", "No active pricing rule is configured.");
        }

        var distance = await routing.GetAsync(origin, destination, ct);
        var amount = rule.Compute(distance.Meters, weightGrams);

        return new PriceEstimate(
            amount,
            rule.Currency,
            Math.Round(distance.Meters, 0),
            distance.Source == DistanceSource.Routed ? "routed" : "geodesic_fallback",
            rule.Id);
    }
}
