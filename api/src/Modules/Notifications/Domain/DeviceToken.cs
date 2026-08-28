using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Notifications.Domain;

public enum DevicePlatform
{
    Ios = 0,
    Android = 1,
}

public sealed class DeviceToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public DevicePlatform Platform { get; set; }

    public required string Token { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>Dedupe ledger so a re-delivered integration event never double-sends a push.</summary>
public sealed class NotificationDispatch
{
    public required string DedupeKey { get; init; }

    public DateTimeOffset SentAt { get; init; }
}

internal sealed class DeviceTokenConfig : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("device_token", "notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Platform).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Token).HasMaxLength(400);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class NotificationDispatchConfig : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> b)
    {
        b.ToTable("notification_dispatch", "notifications");
        b.HasKey(x => x.DedupeKey);
        b.Property(x => x.DedupeKey).HasMaxLength(400);
    }
}
