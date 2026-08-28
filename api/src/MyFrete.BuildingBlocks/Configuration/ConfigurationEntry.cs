namespace MyFrete.BuildingBlocks.Configuration;

/// <summary>
/// A single business parameter editable without a code deploy (FR-009, FR-014, FR-017a,
/// FR-019, FR-022a, FR-025d, FR-012a). See data-model.md §Config.
/// </summary>
public sealed class ConfigurationEntry
{
    public required string Key { get; init; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public static class ConfigKeys
{
    public const string OfferTtlSeconds = "offer_ttl_seconds";
    public const string MaxSearchDurationSeconds = "max_search_duration_seconds";
    public const string MaxProfessionalsContacted = "max_professionals_contacted";
    public const string ScheduleDecisionTimeoutSeconds = "schedule_decision_timeout_seconds";
    public const string SchedulingWindowDays = "scheduling_window_days";
    public const string MaxSchedulesPerDate = "max_schedules_per_date";
    public const string DeliveryVerificationHours = "delivery_verification_hours";
    public const string LocationTtlSeconds = "location_ttl_seconds";
    public const string ImmediateOfferRadiusMeters = "immediate_offer_radius_meters";
    public const string SinuosityFactor = "sinuosity_factor";
    public const string PricingBaseFare = "pricing_base_fare";
    public const string PricingPerKm = "pricing_per_km";
    public const string PricingPerKg = "pricing_per_kg";
    public const string PricingMinPrice = "pricing_min_price";
}

/// <summary>Reads business parameters from the <c>configuration</c> table with a short cache.</summary>
public interface IAppConfiguration
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);

    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);

    Task<TimeSpan> GetSecondsAsync(string key, int fallbackSeconds, CancellationToken ct = default);
}
