using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Contracts;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Domain;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace MyFrete.Modules.Accounts.Features;

public sealed record ProfessionalMeDto(
    decimal MaxLoadKg,
    bool ImmediateAvailability,
    string VerificationStatus,
    DateTimeOffset? LastLocationAt);

// ---------------------------------------------------------------- PATCH /professionals/me (FR-004)

public sealed record UpdateProfessionalCommand(decimal? MaxLoadKg, bool? ImmediateAvailability)
    : ICommand<Result<ProfessionalMeDto>>;

public sealed class UpdateProfessionalValidator : AbstractValidator<UpdateProfessionalCommand>
{
    public UpdateProfessionalValidator()
    {
        RuleFor(x => x.MaxLoadKg).GreaterThan(0).When(x => x.MaxLoadKg.HasValue);
        RuleFor(x => x).Must(x => x.MaxLoadKg.HasValue || x.ImmediateAvailability.HasValue)
            .WithMessage("Provide maxLoadKg and/or immediateAvailability.");
    }
}

public sealed class UpdateProfessionalHandler(
    DbContext db,
    ICurrentActor actor,
    IActiveTripGuard activeTrips,
    TimeProvider clock)
    : IRequestHandler<UpdateProfessionalCommand, Result<ProfessionalMeDto>>
{
    public async Task<Result<ProfessionalMeDto>> Handle(UpdateProfessionalCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("accounts.not_authenticated", "Not authenticated.");
        }

        var profile = await db.Set<ProfessionalProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return Error.NotFound("accounts.not_a_professional", "This account is not registered as a professional.");
        }

        if (cmd.ImmediateAvailability == true && await activeTrips.HasActiveTripAsync(userId, ct))
        {
            return Error.Conflict("accounts.active_trip",
                "Cannot go available while a transport is in progress.");
        }

        if (cmd.MaxLoadKg is { } kg)
        {
            profile.MaxLoadGrams = (int)Math.Round(kg * 1000m);
        }

        if (cmd.ImmediateAvailability is { } available)
        {
            profile.ImmediateAvailability = available;
            if (!available)
            {
                profile.LastLocation = null;
                profile.LastLocationAt = null;
            }
        }

        profile.UpdatedAt = clock.GetUtcNow();

        return new ProfessionalMeDto(
            Math.Round(profile.MaxLoadGrams / 1000m, 3),
            profile.ImmediateAvailability,
            profile.VerificationStatus.ToString(),
            profile.LastLocationAt);
    }
}

// ---------------------------------------------------------------- PATCH /professionals/me/location (FR-012a)

public sealed record UpdateLocationCommand(double Lat, double Lng) : ICommand<Result>;

public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
    }
}

public sealed class UpdateLocationHandler(
    DbContext db,
    ICurrentActor actor,
    IAppConfiguration config,
    TimeProvider clock)
    : IRequestHandler<UpdateLocationCommand, Result>
{
    private static readonly GeometryFactory Geo = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<Result> Handle(UpdateLocationCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("accounts.not_authenticated", "Not authenticated.");
        }

        var profile = await db.Set<ProfessionalProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return Error.NotFound("accounts.not_a_professional", "This account is not registered as a professional.");
        }

        if (!profile.ImmediateAvailability)
        {
            return Error.Conflict("accounts.not_available",
                "Location is only tracked while available for immediate offers.");
        }

        var now = clock.GetUtcNow();
        var minInterval = await config.GetSecondsAsync("min_location_update_interval_seconds", 20, ct);
        if (profile.LastLocationAt is { } last && now - last < minInterval)
        {
            // Accepted but throttled — the client should not send this often.
            return Result.Success();
        }

        profile.LastLocation = Geo.CreatePoint(new Coordinate(cmd.Lng, cmd.Lat));
        profile.LastLocationAt = now;
        profile.UpdatedAt = now;

        return Result.Success();
    }
}
