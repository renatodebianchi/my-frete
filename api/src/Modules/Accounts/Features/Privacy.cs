using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Auth;
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
    ITokenService tokens,
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

        var now = clock.GetUtcNow();
        var request = new DataSubjectRequest
        {
            UserId = userId,
            Kind = kind,
            Details = cmd.Details,
            RequestedAt = now,
        };
        db.Set<DataSubjectRequest>().Add(request);

        // Deletion is fulfilled immediately by anonymising the account and revoking sessions.
        // Operational records (requests/trips) keep the user id for integrity but hold no PII.
        if (kind == DataSubjectRequestKind.Deletion)
        {
            var user = await db.Set<User>().FirstAsync(u => u.Id == userId, ct);
            user.Name = "Usuário removido";
            user.Email = $"deleted+{userId:N}@my-frete.invalid";
            user.Phone = "removido";
            user.Status = UserStatus.DeletionRequested;
            user.PasswordHash = string.Empty;
            user.UpdatedAt = now;

            var refreshTokens = await db.Set<RefreshToken>()
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in refreshTokens)
            {
                token.RevokedAt = now;
            }

            _ = tokens; // reserved for future re-issue flows
            request.Status = DataSubjectRequestStatus.Fulfilled;
            request.ResolvedAt = now;
        }

        outbox.Enqueue(
            new DataSubjectRequestCreated(request.Id, userId, kind.ToString()),
            dedupeKey: $"datasubject.request_created.v1::{request.Id}");

        await audit.WriteAsync("datasubject.request_created", "DataSubjectRequest", request.Id,
            new { kind = kind.ToString(), fulfilled = request.Status == DataSubjectRequestStatus.Fulfilled }, ct: ct);

        return request.Id;
    }
}

// ---------------------------------------------------------------- Rectification (PATCH /accounts/me)

public sealed record UpdateMyProfileCommand(string? Name, string? Phone) : ICommand<Result<MeDto>>;

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(x => x.Name).MinimumLength(2).MaximumLength(120).When(x => x.Name is not null);
        RuleFor(x => x.Phone).MaximumLength(20).When(x => x.Phone is not null);
        RuleFor(x => x).Must(x => x.Name is not null || x.Phone is not null)
            .WithMessage("Provide name and/or phone.");
    }
}

public sealed class UpdateMyProfileHandler(DbContext db, ICurrentActor actor, TimeProvider clock)
    : IRequestHandler<UpdateMyProfileCommand, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(UpdateMyProfileCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("accounts.not_authenticated", "Not authenticated.");
        }

        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return Error.NotFound("accounts.user_not_found", "User not found.");
        }

        if (cmd.Name is not null)
        {
            user.Name = cmd.Name.Trim();
        }

        if (cmd.Phone is not null)
        {
            user.Phone = cmd.Phone.Trim();
        }

        user.UpdatedAt = clock.GetUtcNow();

        ProfessionalDto? pro = null;
        if (user.HasRole(Roles.Professional))
        {
            var p = await db.Set<ProfessionalProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (p is not null)
            {
                pro = new ProfessionalDto(
                    Math.Round(p.MaxLoadGrams / 1000m, 3), p.ImmediateAvailability,
                    p.VerificationStatus.ToString(), p.LastLocationAt);
            }
        }

        return new MeDto(user.Id, user.Name, user.Email, user.Phone, user.Roles, pro);
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
