using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Scheduling.Contracts;
using MyFrete.Modules.Scheduling.Domain;
using MyFrete.Modules.Trips.Contracts;

namespace MyFrete.Modules.Scheduling.Features;

// ---------------------------------------------------------------- availability (T086)

public sealed record GetAvailabilityQuery : IQuery<Result<IReadOnlyList<DateOnly>>>;

public sealed class GetAvailabilityHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<GetAvailabilityQuery, Result<IReadOnlyList<DateOnly>>>
{
    public async Task<Result<IReadOnlyList<DateOnly>>> Handle(GetAvailabilityQuery q, CancellationToken ct)
    {
        if (actor.UserId is not { } id)
        {
            return Error.Unauthorized("scheduling.not_authenticated", "Not authenticated.");
        }

        var dates = await db.Set<ProfessionalScheduleAvailability>()
            .Where(a => a.ProfessionalId == id)
            .OrderBy(a => a.AvailableDate)
            .Select(a => a.AvailableDate)
            .ToListAsync(ct);

        return dates;
    }
}

public sealed record SetAvailabilityCommand(IReadOnlyList<DateOnly> Dates) : ICommand<Result>;

public sealed class SetAvailabilityHandler(DbContext db, ICurrentActor actor, TimeProvider clock)
    : IRequestHandler<SetAvailabilityCommand, Result>
{
    public async Task<Result> Handle(SetAvailabilityCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } id)
        {
            return Error.Unauthorized("scheduling.not_authenticated", "Not authenticated.");
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var wanted = cmd.Dates.Where(d => d > today).Distinct().ToHashSet();

        var current = await db.Set<ProfessionalScheduleAvailability>()
            .Where(a => a.ProfessionalId == id)
            .ToListAsync(ct);

        db.RemoveRange(current.Where(a => !wanted.Contains(a.AvailableDate)));
        foreach (var date in wanted.Where(d => current.All(c => c.AvailableDate != d)))
        {
            db.Add(new ProfessionalScheduleAvailability { ProfessionalId = id, AvailableDate = date });
        }

        return Result.Success();
    }
}

// ---------------------------------------------------------------- scheduled-offer inbox

public sealed record ScheduledOfferDto(Guid Id, Guid RequestId, DateOnly ScheduledDate, decimal WeightKg);

public sealed record ScheduledInboxQuery : IQuery<Result<IReadOnlyList<ScheduledOfferDto>>>;

public sealed class ScheduledInboxHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<ScheduledInboxQuery, Result<IReadOnlyList<ScheduledOfferDto>>>
{
    public async Task<Result<IReadOnlyList<ScheduledOfferDto>>> Handle(ScheduledInboxQuery q, CancellationToken ct)
    {
        if (actor.UserId is not { } id)
        {
            return Error.Unauthorized("scheduling.not_authenticated", "Not authenticated.");
        }

        var rows = await db.Set<ScheduledOffer>()
            .Where(o => o.ProfessionalId == id && o.Outcome == ScheduledOfferOutcome.Pending)
            .OrderBy(o => o.ScheduledDate)
            .Select(o => new ScheduledOfferDto(o.Id, o.RequestId, o.ScheduledDate, o.WeightGrams / 1000m))
            .ToListAsync(ct);

        return rows;
    }
}

// ---------------------------------------------------------------- accept (T088/T089, FR-022)

public sealed record AcceptScheduledOfferCommand(Guid OfferId) : ICommand<Result<AcceptScheduledResult>>;

public sealed record AcceptScheduledResult(Guid TripId, Guid RequestId);

public sealed class AcceptScheduledOfferHandler(
    DbContext db,
    ICurrentActor actor,
    IRequestAssignment requests,
    ITripFactory trips,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<AcceptScheduledOfferCommand, Result<AcceptScheduledResult>>
{
    public async Task<Result<AcceptScheduledResult>> Handle(AcceptScheduledOfferCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } professionalId)
        {
            return Error.Unauthorized("scheduling.not_authenticated", "Not authenticated.");
        }

        var offer = await db.Set<ScheduledOffer>().FirstOrDefaultAsync(o => o.Id == cmd.OfferId, ct);
        if (offer is null || offer.ProfessionalId != professionalId)
        {
            return Error.NotFound("scheduling.offer_not_found", "Offer not found.");
        }

        if (offer.Outcome != ScheduledOfferOutcome.Pending)
        {
            return Error.Conflict("scheduling.offer_closed", "This offer is no longer open.");
        }

        var now = clock.GetUtcNow();

        // First accepter wins: TryAssignAsync only succeeds while the request is scheduled_searching.
        if (!await requests.TryAssignAsync(offer.RequestId, professionalId, ct))
        {
            offer.Outcome = ScheduledOfferOutcome.FilledByOther;
            offer.RespondedAt = now;
            return Error.Conflict("scheduling.already_filled", "This schedule was already taken.");
        }

        var load = await db.Set<ProfessionalDailyLoad>()
            .FirstOrDefaultAsync(l => l.ProfessionalId == professionalId && l.LoadDate == offer.ScheduledDate, ct);
        if (load is null)
        {
            db.Add(new ProfessionalDailyLoad
            {
                ProfessionalId = professionalId,
                LoadDate = offer.ScheduledDate,
                AcceptedCount = 1,
            });
        }
        else
        {
            load.AcceptedCount++;
        }

        offer.Outcome = ScheduledOfferOutcome.Accepted;
        offer.RespondedAt = now;

        var others = await db.Set<ScheduledOffer>()
            .Where(o => o.RequestId == offer.RequestId && o.Id != offer.Id && o.Outcome == ScheduledOfferOutcome.Pending)
            .ToListAsync(ct);
        foreach (var other in others)
        {
            other.Outcome = ScheduledOfferOutcome.FilledByOther;
            other.RespondedAt = now;
            outbox.Enqueue(new ScheduledOfferFilledByOther(other.Id, other.ProfessionalId));
        }

        var tripId = await trips.CreateAsync(
            offer.RequestId, offer.ClientId, professionalId, offer.EstimatedPrice, offer.Currency, ct);

        outbox.Enqueue(new ScheduledOfferAccepted(offer.Id, offer.RequestId, offer.ClientId, professionalId, tripId));
        await audit.WriteAsync("request.assigned", "TransportRequest", offer.RequestId,
            new { professionalId, scheduled = true }, ct: ct);

        return new AcceptScheduledResult(tripId, offer.RequestId);
    }
}
