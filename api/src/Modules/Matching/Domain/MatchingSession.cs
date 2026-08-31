using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Matching.Domain;

public enum SessionState
{
    Searching = 0,
    OfferPending = 1,
    Accepted = 2,
    Exhausted = 3,
}

public enum OfferOutcome
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    FilledByOther = 4,
}

/// <summary>One immediate-search session per request (data-model.md §Matching).</summary>
public sealed class MatchingSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid RequestId { get; init; }

    public Guid ClientId { get; init; }

    public double OriginLat { get; init; }

    public double OriginLng { get; init; }

    public int WeightGrams { get; init; }

    public SessionState State { get; set; } = SessionState.Searching;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset DeadlineAt { get; set; }

    public int ContactedCount { get; set; }

    public string ContactedIdsCsv { get; set; } = string.Empty;

    public Guid? CurrentOfferId { get; set; }

    public IReadOnlySet<Guid> ContactedIds => ContactedIdsCsv.Length == 0
        ? new HashSet<Guid>()
        : ContactedIdsCsv.Split(',').Select(Guid.Parse).ToHashSet();

    public void AddContacted(Guid professionalId)
    {
        var set = new HashSet<Guid>(ContactedIds) { professionalId };
        ContactedIdsCsv = string.Join(',', set);
        ContactedCount = set.Count;
    }
}

public sealed class Offer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SessionId { get; init; }

    public Guid RequestId { get; init; }

    public Guid ProfessionalId { get; init; }

    public DateTimeOffset SentAt { get; init; }

    public DateTimeOffset RespondBy { get; init; }

    public OfferOutcome Outcome { get; set; } = OfferOutcome.Pending;

    public DateTimeOffset? RespondedAt { get; set; }

    public bool IsOpen(DateTimeOffset now) => Outcome == OfferOutcome.Pending && now <= RespondBy;
}

internal sealed class MatchingSessionConfig : IEntityTypeConfiguration<MatchingSession>
{
    public void Configure(EntityTypeBuilder<MatchingSession> b)
    {
        b.ToTable("matching_session", "matching");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => x.State);
        b.Ignore(x => x.ContactedIds);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ContactedIdsCsv).HasColumnName("contacted_ids").HasMaxLength(4000);
    }
}

internal sealed class OfferConfig : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("offer", "matching");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SessionId);
        b.HasIndex(x => new { x.ProfessionalId, x.Outcome });
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
    }
}
