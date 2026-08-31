using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Matching.Contracts;
using MyFrete.Modules.Matching.Domain;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;
using MyFrete.Modules.Trips.Contracts;

namespace MyFrete.Modules.Matching.Features;

public sealed record OfferInboxDto(
    Guid Id,
    Guid RequestId,
    DateTimeOffset RespondBy,
    OfferSummaryDto Summary);

public sealed record OfferSummaryDto(
    string OriginAddress,
    string DestinationAddress,
    double DistanceKm,
    decimal EstimatedWeightKg,
    decimal EstimatedAmount);

// ---------------------------------------------------------------- GET /offers/inbox (T064)

public sealed record OfferInboxQuery : IQuery<Result<IReadOnlyList<OfferInboxDto>>>;

public sealed class OfferInboxHandler(DbContext db, ICurrentActor actor, TimeProvider clock)
    : IRequestHandler<OfferInboxQuery, Result<IReadOnlyList<OfferInboxDto>>>
{
    public async Task<Result<IReadOnlyList<OfferInboxDto>>> Handle(OfferInboxQuery q, CancellationToken ct)
    {
        if (actor.UserId is not { } professionalId)
        {
            return Error.Unauthorized("matching.not_authenticated", "Not authenticated.");
        }

        var now = clock.GetUtcNow();
        var rows = await (
            from o in db.Set<Offer>()
            join r in db.Set<TransportRequest>() on o.RequestId equals r.Id
            where o.ProfessionalId == professionalId && o.Outcome == OfferOutcome.Pending && o.RespondBy > now
            orderby o.SentAt
            select new OfferInboxDto(
                o.Id,
                o.RequestId,
                o.RespondBy,
                new OfferSummaryDto(
                    r.OriginAddress,
                    r.DestinationAddress,
                    Math.Round(r.DistanceMeters / 1000.0, 2),
                    r.EstimatedWeightGrams / 1000m,
                    r.EstimatedPrice)))
            .ToListAsync(ct);

        return rows;
    }
}

// ---------------------------------------------------------------- POST /offers/{id}/accept (T063)

public sealed record AcceptOfferCommand(Guid OfferId) : ICommand<Result<AcceptResult>>;

public sealed record AcceptResult(Guid TripId, Guid RequestId);

public sealed class AcceptOfferHandler(
    DbContext db,
    ICurrentActor actor,
    IRequestAssignment requests,
    ITripFactory trips,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<AcceptOfferCommand, Result<AcceptResult>>
{
    public async Task<Result<AcceptResult>> Handle(AcceptOfferCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } professionalId)
        {
            return Error.Unauthorized("matching.not_authenticated", "Not authenticated.");
        }

        var offer = await db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == cmd.OfferId, ct);
        if (offer is null || offer.ProfessionalId != professionalId)
        {
            return Error.NotFound("matching.offer_not_found", "Offer not found.");
        }

        var now = clock.GetUtcNow();
        if (!offer.IsOpen(now))
        {
            return Error.Conflict("matching.offer_closed", "This offer is no longer open.");
        }

        var request = await db.Set<TransportRequest>().FirstOrDefaultAsync(r => r.Id == offer.RequestId, ct);
        if (request is null)
        {
            return Error.NotFound("matching.request_not_found", "Request not found.");
        }

        if (!await requests.TryAssignAsync(offer.RequestId, professionalId, ct))
        {
            offer.Outcome = OfferOutcome.FilledByOther;
            offer.RespondedAt = now;
            return Error.Conflict("matching.already_assigned", "This request was already taken.");
        }

        var tripId = await trips.CreateAsync(
            offer.RequestId, request.ClientId, professionalId, request.EstimatedPrice, request.Currency, ct);

        offer.Outcome = OfferOutcome.Accepted;
        offer.RespondedAt = now;

        var session = await db.Set<MatchingSession>().FirstOrDefaultAsync(s => s.Id == offer.SessionId, ct);
        if (session is not null)
        {
            session.State = SessionState.Accepted;
            session.CurrentOfferId = null;
        }

        outbox.Enqueue(new OfferAccepted(offer.Id, offer.RequestId, professionalId, tripId));
        await audit.WriteAsync("request.assigned", "TransportRequest", offer.RequestId,
            new { professionalId, offerId = offer.Id }, ct: ct);

        return new AcceptResult(tripId, offer.RequestId);
    }
}

// ---------------------------------------------------------------- POST /offers/{id}/decline

public sealed record DeclineOfferCommand(Guid OfferId) : ICommand<Result>;

public sealed class DeclineOfferHandler(
    DbContext db,
    ICurrentActor actor,
    IOutboxWriter outbox,
    TimeProvider clock) : IRequestHandler<DeclineOfferCommand, Result>
{
    public async Task<Result> Handle(DeclineOfferCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } professionalId)
        {
            return Error.Unauthorized("matching.not_authenticated", "Not authenticated.");
        }

        var offer = await db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == cmd.OfferId, ct);
        if (offer is null || offer.ProfessionalId != professionalId)
        {
            return Error.NotFound("matching.offer_not_found", "Offer not found.");
        }

        if (offer.Outcome != OfferOutcome.Pending)
        {
            return Result.Success();
        }

        var now = clock.GetUtcNow();
        offer.Outcome = OfferOutcome.Declined;
        offer.RespondedAt = now;

        var session = await db.Set<MatchingSession>().FirstOrDefaultAsync(s => s.Id == offer.SessionId, ct);
        if (session is { State: SessionState.OfferPending })
        {
            session.State = SessionState.Searching;
            session.CurrentOfferId = null;
        }

        outbox.Enqueue(new OfferDeclined(offer.Id, professionalId));
        return Result.Success();
    }
}
