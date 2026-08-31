namespace MyFrete.Modules.Accounts.Domain;

public static class Roles
{
    public const string Client = "client";
    public const string Professional = "professional";
}

public enum UserStatus
{
    Active = 0,
    Suspended = 1,
    DeletionRequested = 2,
}

public sealed class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string Email { get; set; }

    public required string Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public UserStatus Status { get; set; } = UserStatus.Active;

    public int FailedAccessCount { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } end && end > now;

    public bool HasRole(string role) => Roles.Contains(role);
}

public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public required string TokenHash { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
