using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Persistence;

internal sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("user", "accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Roles).HasColumnType("text[]");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
    }
}

internal sealed class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_token", "accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(128);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class ClientProfileConfig : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> b)
    {
        b.ToTable("client_profile", "accounts");
        b.HasKey(x => x.UserId);
    }
}

internal sealed class ProfessionalProfileConfig : IEntityTypeConfiguration<ProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfile> b)
    {
        b.ToTable("professional_profile", "accounts");
        b.HasKey(x => x.UserId);
        b.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.LastLocation).HasColumnType("geography (point, 4326)");
        b.HasIndex(x => x.LastLocation).HasMethod("gist");
        b.HasIndex(x => x.ImmediateAvailability).HasFilter("immediate_availability");
    }
}

internal sealed class VerificationEventConfig : IEntityTypeConfiguration<VerificationEvent>
{
    public void Configure(EntityTypeBuilder<VerificationEvent> b)
    {
        b.ToTable("verification_event", "accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Actor).HasMaxLength(200);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.ProfessionalId);
    }
}
