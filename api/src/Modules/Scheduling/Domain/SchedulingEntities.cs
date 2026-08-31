using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Scheduling.Domain;

public sealed class ProfessionalScheduleAvailability
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ProfessionalId { get; init; }

    public DateOnly AvailableDate { get; init; }
}

/// <summary>Per-date accepted count (FR-022a: at most N scheduled jobs per date).</summary>
public sealed class ProfessionalDailyLoad
{
    public Guid ProfessionalId { get; init; }

    public DateOnly LoadDate { get; init; }

    public int AcceptedCount { get; set; }
}

public enum ScheduledOfferOutcome
{
    Pending = 0,
    Accepted = 1,
    FilledByOther = 2,
    Unfulfilled = 3,
}

public sealed class ScheduledOffer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid RequestId { get; init; }

    public Guid ClientId { get; init; }

    public Guid ProfessionalId { get; init; }

    public DateOnly ScheduledDate { get; init; }

    public int WeightGrams { get; init; }

    public decimal EstimatedPrice { get; init; }

    public string Currency { get; init; } = "BRL";

    public ScheduledOfferOutcome Outcome { get; set; } = ScheduledOfferOutcome.Pending;

    public DateTimeOffset SentAt { get; init; }

    public DateTimeOffset? RespondedAt { get; set; }
}

internal sealed class AvailabilityConfig : IEntityTypeConfiguration<ProfessionalScheduleAvailability>
{
    public void Configure(EntityTypeBuilder<ProfessionalScheduleAvailability> b)
    {
        b.ToTable("professional_schedule_availability", "scheduling");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProfessionalId, x.AvailableDate }).IsUnique();
        b.HasIndex(x => x.AvailableDate);
    }
}

internal sealed class DailyLoadConfig : IEntityTypeConfiguration<ProfessionalDailyLoad>
{
    public void Configure(EntityTypeBuilder<ProfessionalDailyLoad> b)
    {
        b.ToTable("professional_daily_load", "scheduling");
        b.HasKey(x => new { x.ProfessionalId, x.LoadDate });
    }
}

internal sealed class ScheduledOfferConfig : IEntityTypeConfiguration<ScheduledOffer>
{
    public void Configure(EntityTypeBuilder<ScheduledOffer> b)
    {
        b.ToTable("scheduled_offer", "scheduling");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RequestId, x.Outcome });
        b.HasIndex(x => new { x.ProfessionalId, x.Outcome });
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EstimatedPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3);
    }
}
