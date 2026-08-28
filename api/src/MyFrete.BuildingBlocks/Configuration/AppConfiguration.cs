using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MyFrete.BuildingBlocks.Configuration;

/// <summary>
/// Reads business parameters from the <c>configuration</c> table, cached ~30s so operators can
/// change values without a deploy (FR-009, FR-014, FR-017a, FR-019, FR-022a, FR-025d, FR-012a).
/// </summary>
public sealed class AppConfiguration(DbContext db, IMemoryCache cache) : IAppConfiguration
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw is not null && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    public async Task<TimeSpan> GetSecondsAsync(string key, int fallbackSeconds, CancellationToken ct = default) =>
        TimeSpan.FromSeconds(await GetIntAsync(key, fallbackSeconds, ct));

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
    {
        if (cache.TryGetValue<string?>(CacheKey(key), out var cached))
        {
            return cached;
        }

        var value = await db.Set<ConfigurationEntry>()
            .Where(e => e.Key == key)
            .Select(e => e.Value)
            .FirstOrDefaultAsync(ct);

        cache.Set(CacheKey(key), value, CacheTtl);
        return value;
    }

    private static string CacheKey(string key) => $"config:{key}";
}
