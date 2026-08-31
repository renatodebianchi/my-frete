using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Features;

public sealed record ProfessionalDto(
    decimal MaxLoadKg,
    bool ImmediateAvailability,
    string VerificationStatus,
    DateTimeOffset? LastLocationAt);

public sealed record MeDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    IReadOnlyList<string> Roles,
    ProfessionalDto? Professional);

public sealed record GetMeQuery : IQuery<Result<MeDto>>;

public sealed class GetMeHandler(DbContext db, ICurrentActor actor) : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken ct)
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

        ProfessionalDto? pro = null;
        if (user.HasRole(Roles.Professional))
        {
            var p = await db.Set<ProfessionalProfile>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (p is not null)
            {
                pro = new ProfessionalDto(
                    Math.Round(p.MaxLoadGrams / 1000m, 3),
                    p.ImmediateAvailability,
                    p.VerificationStatus.ToString(),
                    p.LastLocationAt);
            }
        }

        return new MeDto(user.Id, user.Name, user.Email, user.Phone, user.Roles, pro);
    }
}
