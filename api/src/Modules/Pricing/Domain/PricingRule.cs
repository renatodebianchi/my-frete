using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Pricing.Domain;

/// <summary>
/// Configurable price formula (FR-009). Estimate = max(min_price,
/// base_fare + per_km * km + per_kg * kg). Only one rule is in effect at a time.
/// </summary>
public sealed class PricingRule
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public decimal BaseFare { get; init; }

    public decimal PerKm { get; init; }

    public decimal PerKg { get; init; }

    public decimal MinPrice { get; init; }

    public string Currency { get; init; } = "BRL";

    public DateTimeOffset EffectiveFrom { get; init; }

    public DateTimeOffset? EffectiveTo { get; init; }

    public decimal Compute(double distanceMeters, int weightGrams)
    {
        var km = (decimal)(distanceMeters / 1000.0);
        var kg = weightGrams / 1000m;
        var raw = BaseFare + (PerKm * km) + (PerKg * kg);
        return Math.Round(Math.Max(MinPrice, raw), 2, MidpointRounding.AwayFromZero);
    }
}

internal sealed class PricingRuleConfig : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> b)
    {
        b.ToTable("pricing_rule", "pricing");
        b.HasKey(x => x.Id);
        b.Property(x => x.BaseFare).HasColumnType("numeric(12,2)");
        b.Property(x => x.PerKm).HasColumnType("numeric(12,2)");
        b.Property(x => x.PerKg).HasColumnType("numeric(12,2)");
        b.Property(x => x.MinPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => x.EffectiveFrom);
    }
}
