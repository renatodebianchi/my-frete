using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.Modules.Pricing.Domain;

namespace MyFrete.Api.Cli;

/// <summary>
/// `dotnet MyFrete.Api.dll seed --demo` — seeds a demo pricing rule and the default business
/// parameters so a fresh database can serve estimates and matching.
/// </summary>
public static class SeedCommand
{
    public const string Verb = "seed";

    private static readonly (string Key, string Value)[] DefaultConfig =
    [
        (ConfigKeys.OfferTtlSeconds, "30"),
        (ConfigKeys.MaxSearchDurationSeconds, "300"),
        (ConfigKeys.MaxProfessionalsContacted, "8"),
        (ConfigKeys.ScheduleDecisionTimeoutSeconds, "600"),
        (ConfigKeys.SchedulingWindowDays, "30"),
        (ConfigKeys.MaxSchedulesPerDate, "1"),
        (ConfigKeys.DeliveryVerificationHours, "24"),
        (ConfigKeys.LocationTtlSeconds, "300"),
        (ConfigKeys.ImmediateOfferRadiusMeters, "15000"),
        (ConfigKeys.SinuosityFactor, "1.3"),
        ("min_location_update_interval_seconds", "20"),
    ];

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, CancellationToken ct)
    {
        var demo = args.Contains("--demo");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var now = DateTimeOffset.UtcNow;

        foreach (var (key, value) in DefaultConfig)
        {
            if (!await db.Set<ConfigurationEntry>().AnyAsync(c => c.Key == key, ct))
            {
                db.Set<ConfigurationEntry>().Add(new ConfigurationEntry { Key = key, Value = value, UpdatedAt = now });
            }
        }

        if (demo && !await db.Set<PricingRule>().AnyAsync(ct))
        {
            db.Set<PricingRule>().Add(new PricingRule
            {
                BaseFare = 12.00m,
                PerKm = 2.50m,
                PerKg = 0.15m,
                MinPrice = 20.00m,
                Currency = "BRL",
                EffectiveFrom = now.AddYears(-1),
            });
            Console.WriteLine("[seed] demo pricing rule added.");
        }

        await db.SaveChangesAsync(ct);
        Console.WriteLine("[seed] configuration defaults ensured.");
        return 0;
    }
}
