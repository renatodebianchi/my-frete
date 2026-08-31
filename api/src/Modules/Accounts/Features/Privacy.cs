using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Features;

[EventType("datasubject.request_created.v1")]
public sealed record DataSubjectRequestCreated(Guid AggregateId, Guid UserId, string Kind) : IIntegrationEvent
{
    public string AggregateType => "DataSubjectRequest";
}

// ---------------------------------------------------------------- Create request (FR-030)

public sealed record CreateDataSubjectRequestCommand(string Kind, string? Details) : ICommand<Result<Guid>>;

public sealed class CreateDataSubjectRequestHandler(
    DbContext db,
    ICurrentActor actor,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock)
    : IRequestHandler<CreateDataSubjectRequestCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDataSubjectRequestCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("accounts.not_authenticated", "Not authenticated.");
        }

        if (!Enum.TryParse<DataSubjectRequestKind>(cmd.Kind, ignoreCase: true, out var kind))
        {
            return Error.Validation("privacy.invalid_kind", "kind must be access, rectification or deletion.");
        }

        var request = new DataSubjectRequest
        {
            UserId = userId,
            Kind = kind,
            Details = cmd.Details,
            RequestedAt = clock.GetUtcNow(),
        };
        db.Set<DataSubjectRequest>().Add(request);

        outbox.Enqueue(
            new DataSubjectRequestCreated(request.Id, userId, kind.ToString()),
            dedupeKey: $"datasubject.request_created.v1::{request.Id}");

        await audit.WriteAsync("datasubject.request_created", "DataSubjectRequest", request.Id,
            new { kind = kind.ToString() }, ct: ct);

        return request.Id;
    }
}

// ---------------------------------------------------------------- Self-service export (FR-030, T032b)

public sealed record ExportMyDataQuery : IQuery<Result<DataExport>>;

public sealed record DataExport(
    object Account,
    object? Professional,
    IReadOnlyList<object> PrivacyRequests);

public sealed class ExportMyDataHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<ExportMyDataQuery, Result<DataExport>>
{
    public async Task<Result<DataExport>> Handle(ExportMyDataQuery request, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("accounts.not_authenticated", "Not authenticated.");
        }

        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return Error.NotFound("accounts.user_not_found", "User not found.");
        }

        var pro = await db.Set<ProfessionalProfile>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var privacy = await db.Set<DataSubjectRequest>().AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { r.Id, kind = r.Kind.ToString(), status = r.Status.ToString(), r.RequestedAt, r.ResolvedAt })
            .ToListAsync(ct);

        return new DataExport(
            Account: new { user.Id, user.Name, user.Email, user.Phone, user.Roles, status = user.Status.ToString(), user.CreatedAt },
            Professional: pro is null
                ? null
                : new { maxLoadKg = Math.Round(pro.MaxLoadGrams / 1000m, 3), pro.ImmediateAvailability, verificationStatus = pro.VerificationStatus.ToString() },
            PrivacyRequests: privacy.Cast<object>().ToList());
    }
}
