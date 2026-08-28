namespace MyFrete.Modules.Accounts.Domain;

public sealed class ClientProfile
{
    public Guid UserId { get; init; }
}

public enum VerificationStatus
{
    NaoVerificado = 0,
    EmAnalise = 1,
    Verificado = 2,
    Rejeitado = 3,
}

/// <summary>
/// MVP: activated with self-declared data (FR-005). Location fields and schedule availability
/// are added in the professional-onboarding phase (US3).
/// </summary>
public sealed class ProfessionalProfile
{
    public Guid UserId { get; init; }

    public int MaxLoadGrams { get; set; }

    public bool ImmediateAvailability { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.NaoVerificado;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static ProfessionalProfile Create(Guid userId, decimal maxLoadKg, DateTimeOffset now) => new()
    {
        UserId = userId,
        MaxLoadGrams = (int)Math.Round(maxLoadKg * 1000m),
        CreatedAt = now,
        UpdatedAt = now,
    };
}

public sealed class VerificationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ProfessionalId { get; init; }

    public VerificationStatus FromStatus { get; init; }

    public VerificationStatus ToStatus { get; init; }

    public string? Reason { get; init; }

    public required string Actor { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
