using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Professionals;

[EventType("professional.verification_changed.v1")]
public sealed record ProfessionalVerificationChanged(Guid AggregateId, string From, string To, string Actor)
    : IIntegrationEvent
{
    public string AggregateType => "ProfessionalProfile";
}

/// <summary>
/// Extension point for a future verification workflow (manual operator review or an automated
/// document/CNH check — research.md §7). No-op in the MVP: professionals stay `nao_verificado`.
/// </summary>
public interface IVerificationProvider
{
    Task<VerificationStatus> EvaluateAsync(Guid professionalId, CancellationToken ct = default);
}

public sealed class NoOpVerificationProvider : IVerificationProvider
{
    public Task<VerificationStatus> EvaluateAsync(Guid professionalId, CancellationToken ct = default) =>
        Task.FromResult(VerificationStatus.NaoVerificado);
}

/// <summary>Applies a verification-status change, recording the event trail (FR-005).</summary>
public sealed class VerificationService(DbContext db, IOutboxWriter outbox, IAuditLog audit, TimeProvider clock)
{
    public async Task ChangeStatusAsync(
        ProfessionalProfile professional,
        VerificationStatus to,
        string actor,
        string? reason,
        CancellationToken ct = default)
    {
        var from = professional.VerificationStatus;
        if (from == to)
        {
            return;
        }

        var now = clock.GetUtcNow();
        professional.VerificationStatus = to;
        professional.UpdatedAt = now;

        db.Set<VerificationEvent>().Add(new VerificationEvent
        {
            ProfessionalId = professional.UserId,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            Actor = actor,
            OccurredAt = now,
        });

        outbox.Enqueue(new ProfessionalVerificationChanged(
            professional.UserId, from.ToString(), to.ToString(), actor));

        await audit.WriteAsync("verification.changed", "ProfessionalProfile", professional.UserId,
            new { from = from.ToString(), to = to.ToString() }, actorOverride: actor, ct: ct);
    }
}
