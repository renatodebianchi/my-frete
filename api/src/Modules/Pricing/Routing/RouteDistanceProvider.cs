using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;

namespace MyFrete.Modules.Pricing.Routing;

public readonly record struct GeoPoint(double Lat, double Lng);

public enum DistanceSource
{
    Routed,
    GeodesicFallback,
}

public readonly record struct RouteDistance(double Meters, DistanceSource Source);

/// <summary>Road distance between two points (FR-008). Degrades to a geodesic estimate (§VI).</summary>
public interface IRouteDistanceProvider
{
    Task<RouteDistance> GetAsync(GeoPoint origin, GeoPoint destination, CancellationToken ct = default);
}

/// <summary>
/// Tries the external routing provider (if a key is configured) with resilience, then falls
/// back to Haversine × a configurable sinuosity factor.
/// </summary>
public sealed class ResilientRouteDistanceProvider(
    IExternalRouteClient? external,
    IAppConfiguration config,
    ILogger<ResilientRouteDistanceProvider> logger) : IRouteDistanceProvider
{
    public async Task<RouteDistance> GetAsync(GeoPoint origin, GeoPoint destination, CancellationToken ct = default)
    {
        if (external is not null)
        {
            try
            {
                var meters = await external.GetDistanceMetersAsync(origin, destination, ct);
                if (meters is > 0)
                {
                    return new RouteDistance(meters.Value, DistanceSource.Routed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Route provider failed; falling back to geodesic distance");
            }
        }

        var factor = (double)await config.GetDecimalAsync(ConfigKeys.SinuosityFactor, 1.3m, ct);
        return new RouteDistance(Haversine(origin, destination) * factor, DistanceSource.GeodesicFallback);
    }

    private static double Haversine(GeoPoint a, GeoPoint b)
    {
        const double r = 6_371_000;
        var dLat = ToRad(b.Lat - a.Lat);
        var dLng = ToRad(b.Lng - a.Lng);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + (Math.Cos(ToRad(a.Lat)) * Math.Cos(ToRad(b.Lat)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2));
        return r * (2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h)));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}

/// <summary>Thin wrapper over the chosen matrix API (Google Distance Matrix / Mapbox).</summary>
public interface IExternalRouteClient
{
    Task<double?> GetDistanceMetersAsync(GeoPoint origin, GeoPoint destination, CancellationToken ct);
}

/// <summary>
/// Placeholder external client — returns null so the resilient provider uses the geodesic
/// fallback. Replace with a real Distance Matrix call once a provider/key is chosen.
/// </summary>
public sealed class NullExternalRouteClient : IExternalRouteClient
{
    public Task<double?> GetDistanceMetersAsync(GeoPoint origin, GeoPoint destination, CancellationToken ct) =>
        Task.FromResult<double?>(null);
}
