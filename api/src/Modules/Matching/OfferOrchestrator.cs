using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Accounts.Professionals;
using MyFrete.Modules.Matching.Contracts;
using MyFrete.Modules.Matching.Domain;
using MyFrete.Modules.Requests.Contracts;

namespace MyFrete.Modules.Matching;

/// <summary>
/// Drives the "one offer at a time, 30 s each" flow (FR-013/014/017a). Polls every second so
/// the offer window stays accurate to ~1 s (SC-004). Stateless — safe on multiple replicas
/// (each session row is claimed with a row lock inside the transaction).
/// </summary>
public sealed class OfferOrchestrator(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<OfferOrchestrator> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Offer orchestration tick failed");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var directory = scope.ServiceProvider.GetRequiredService<IProfessionalDirectory>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var requests = scope.ServiceProvider.GetRequiredService<IRequestAssignment>();
        var config = scope.ServiceProvider.GetRequiredService<IAppConfiguration>();

        var now = clock.GetUtcNow();
        var offerTtl = await config.GetSecondsAsync(ConfigKeys.OfferTtlSeconds, 30, ct);
        var maxProfessionals = await config.GetIntAsync(ConfigKeys.MaxProfessionalsContacted, 8, ct);
        var locationTtl = await config.GetSecondsAsync(ConfigKeys.LocationTtlSeconds, 300, ct);
        var radius = await config.GetDecimalAsync(ConfigKeys.ImmediateOfferRadiusMeters, 15000m, ct);

        var active = await db.Set<MatchingSession>()
            .Where(s => s.State == SessionState.Searching || s.State == SessionState.OfferPending)
            .OrderBy(s => s.StartedAt)
            .Take(50)
            .ToListAsync(ct);

        var changed = false;

        foreach (var session in active)
        {
            if (session.State == SessionState.OfferPending)
            {
                var offer = await db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == session.CurrentOfferId, ct);
                if (offer is { Outcome: OfferOutcome.Pending } && now > offer.RespondBy)
                {
                    offer.Outcome = OfferOutcome.Expired;
                    offer.RespondedAt = now;
                    outbox.Enqueue(new OfferExpired(offer.Id, offer.ProfessionalId));
                    session.State = SessionState.Searching;
                    session.CurrentOfferId = null;
                    changed = true;
                }
                else if (offer is null || offer.Outcome != OfferOutcome.Pending)
                {
                    // Accepted/declined out-of-band; let the next branch re-evaluate.
                    session.State = offer?.Outcome == OfferOutcome.Accepted ? SessionState.Accepted : SessionState.Searching;
                    if (session.State == SessionState.Searching)
                    {
                        session.CurrentOfferId = null;
                    }

                    changed = true;
                }
            }

            if (session.State != SessionState.Searching)
            {
                continue;
            }

            if (now > session.DeadlineAt || session.ContactedCount >= maxProfessionals)
            {
                await ExhaustAsync(session, requests, outbox, "limit_reached", ct);
                changed = true;
                continue;
            }

            var eligible = await directory.GetEligibleOrderedByProximityAsync(
                session.WeightGrams, session.OriginLat, session.OriginLng, locationTtl, (double)radius, ct);

            var next = eligible.FirstOrDefault(e => !session.ContactedIds.Contains(e.ProfessionalId));
            if (next is null)
            {
                await ExhaustAsync(session, requests, outbox, "no_eligible_professional", ct);
                changed = true;
                continue;
            }

            var newOffer = new Offer
            {
                SessionId = session.Id,
                RequestId = session.RequestId,
                ProfessionalId = next.ProfessionalId,
                SentAt = now,
                RespondBy = now + offerTtl,
            };
            db.Set<Offer>().Add(newOffer);
            session.State = SessionState.OfferPending;
            session.CurrentOfferId = newOffer.Id;
            session.AddContacted(next.ProfessionalId);
            outbox.Enqueue(new OfferSent(newOffer.Id, session.RequestId, next.ProfessionalId, newOffer.RespondBy));
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task ExhaustAsync(
        MatchingSession session,
        IRequestAssignment requests,
        IOutboxWriter outbox,
        string reason,
        CancellationToken ct)
    {
        session.State = SessionState.Exhausted;
        session.CurrentOfferId = null;
        outbox.Enqueue(new MatchingExhausted(session.RequestId, reason));
        await requests.MarkExhaustedAsync(session.RequestId, ct);
    }
}
