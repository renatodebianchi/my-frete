using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Accounts.Domain;

public enum DataSubjectRequestKind
{
    Access = 0,
    Rectification = 1,
    Deletion = 2,
}

public enum DataSubjectRequestStatus
{
    Open = 0,
    Fulfilled = 1,
    Rejected = 2,
}

/// <summary>LGPD/GDPR data-subject request (FR-030, Constitution §III).</summary>
public sealed class DataSubjectRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public DataSubjectRequestKind Kind { get; init; }

    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Open;

    public string? Details { get; init; }

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; set; }
}

internal sealed class DataSubjectRequestConfig : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> b)
    {
        b.ToTable("data_subject_request", "accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Details).HasMaxLength(2000);
        b.HasIndex(x => new { x.UserId, x.Status });
    }
}
