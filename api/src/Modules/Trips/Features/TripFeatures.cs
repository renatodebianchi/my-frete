using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Trips.Contracts;
using MyFrete.Modules.Trips.Domain;

namespace MyFrete.Modules.Trips.Features;

public sealed record TripDto(
    Guid Id,
    Guid RequestId,
    string Status,
    decimal AgreedAmount,
    string Currency,
    DateTimeOffset? DeliveredAt,
    string? ClientResponse,
    bool PaymentSettledOutsideApp);

public static class TripMapping
{
    public static TripDto ToDto(this Trip t) => new(
        t.Id, t.RequestId, t.Status.ToString().ToLowerInvariant(), t.AgreedAmount, t.Currency,
        t.DeliveredAt, t.ClientResponse?.ToString().ToLowerInvariant(), t.PaymentSettledOutsideApp);
}

// ---------------------------------------------------------------- helpers

internal static class TripAccess
{
    public static async Task<(Trip? Trip, bool IsClient, bool IsProfessional)> LoadAsync(
        DbContext db, Guid tripId, Guid? userId, CancellationToken ct)
    {
        var trip = await db.Set<Trip>().FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip is null || userId is null)
        {
            return (null, false, false);
        }

        return (trip, trip.ClientId == userId, trip.ProfessionalId == userId);
    }
}

// ---------------------------------------------------------------- GET

public sealed record GetTripQuery(Guid Id) : IQuery<Result<TripDto>>;

public sealed class GetTripHandler(DbContext db, ICurrentActor actor) : IRequestHandler<GetTripQuery, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(GetTripQuery q, CancellationToken ct)
    {
        var (trip, isClient, isPro) = await TripAccess.LoadAsync(db, q.Id, actor.UserId, ct);
        return trip is null || !(isClient || isPro)
            ? Error.NotFound("trips.not_found", "Trip not found.")
            : trip.ToDto();
    }
}

public sealed record ListTripsQuery(int Page) : IQuery<Result<IReadOnlyList<TripDto>>>;

public sealed class ListTripsHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<ListTripsQuery, Result<IReadOnlyList<TripDto>>>
{
    public async Task<Result<IReadOnlyList<TripDto>>> Handle(ListTripsQuery q, CancellationToken ct)
    {
        if (actor.UserId is not { } userId)
        {
            return Error.Unauthorized("trips.not_authenticated", "Not authenticated.");
        }

        var page = Math.Max(1, q.Page);
        var items = await db.Set<Trip>().AsNoTracking()
            .Where(t => t.ProfessionalId == userId || t.ClientId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync(ct);

        return items.Select(t => t.ToDto()).ToList();
    }
}

// ---------------------------------------------------------------- PATCH agreed-amount (FR-025)

public sealed record SetAgreedAmountCommand(Guid Id, decimal Amount) : ICommand<Result<TripDto>>;

public sealed class SetAgreedAmountValidator : AbstractValidator<SetAgreedAmountCommand>
{
    public SetAgreedAmountValidator() => RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
}

public sealed class SetAgreedAmountHandler(DbContext db, ICurrentActor actor, TimeProvider clock)
    : IRequestHandler<SetAgreedAmountCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(SetAgreedAmountCommand cmd, CancellationToken ct)
    {
        var (trip, isClient, isPro) = await TripAccess.LoadAsync(db, cmd.Id, actor.UserId, ct);
        if (trip is null || !(isClient || isPro))
        {
            return Error.NotFound("trips.not_found", "Trip not found.");
        }

        return trip.TrySetAgreedAmount(cmd.Amount, clock.GetUtcNow())
            ? trip.ToDto()
            : Error.Conflict("trips.not_editable", "The agreed amount can only change before completion.");
    }
}

// ---------------------------------------------------------------- deliver (FR-025b)

public sealed record DeliverTripCommand(Guid Id) : ICommand<Result<TripDto>>;

public sealed class DeliverTripHandler(
    DbContext db,
    ICurrentActor actor,
    IRequestAssignment requests,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<DeliverTripCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(DeliverTripCommand cmd, CancellationToken ct)
    {
        var (trip, _, isPro) = await TripAccess.LoadAsync(db, cmd.Id, actor.UserId, ct);
        if (trip is null || !isPro)
        {
            return Error.NotFound("trips.not_found", "Trip not found.");
        }

        if (!trip.TryDeliver(clock.GetUtcNow()))
        {
            return Error.Conflict("trips.not_deliverable", $"A trip in '{trip.Status}' cannot be marked delivered.");
        }

        await requests.MarkCompletedAsync(trip.RequestId, ct);
        outbox.Enqueue(new TripDelivered(trip.Id, trip.RequestId, trip.ClientId, trip.ProfessionalId));
        await audit.WriteAsync("trip.delivered", "Trip", trip.Id, ct: ct);

        return trip.ToDto();
    }
}

// ---------------------------------------------------------------- client-response (FR-025c)

public sealed record ClientRespondCommand(Guid Id, string Response, string? Note) : ICommand<Result<TripDto>>;

public sealed class ClientRespondValidator : AbstractValidator<ClientRespondCommand>
{
    public ClientRespondValidator() =>
        RuleFor(x => x.Response).Must(r => r is "confirm" or "dispute")
            .WithMessage("response must be 'confirm' or 'dispute'.");
}

public sealed class ClientRespondHandler(
    DbContext db,
    ICurrentActor actor,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<ClientRespondCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(ClientRespondCommand cmd, CancellationToken ct)
    {
        var (trip, isClient, _) = await TripAccess.LoadAsync(db, cmd.Id, actor.UserId, ct);
        if (trip is null || !isClient)
        {
            return Error.NotFound("trips.not_found", "Trip not found.");
        }

        var response = cmd.Response == "confirm" ? ClientDeliveryResponse.Confirmada : ClientDeliveryResponse.Contestada;
        if (!trip.TryClientRespond(response, clock.GetUtcNow()))
        {
            return Error.Conflict("trips.not_awaiting_response", "This trip is not awaiting a client response.");
        }

        outbox.Enqueue(new TripClientResponded(trip.Id, trip.ClientId, trip.ProfessionalId, response.ToString().ToLowerInvariant()));
        await audit.WriteAsync(
            response == ClientDeliveryResponse.Contestada ? "trip.disputed" : "trip.confirmed",
            "Trip", trip.Id, cmd.Note is null ? null : new { note = cmd.Note }, ct: ct);

        return trip.ToDto();
    }
}

// ---------------------------------------------------------------- cancel (FR-027)

public sealed record CancelTripCommand(Guid Id) : ICommand<Result<TripDto>>;

public sealed class CancelTripHandler(
    DbContext db,
    ICurrentActor actor,
    IRequestAssignment requests,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<CancelTripCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(CancelTripCommand cmd, CancellationToken ct)
    {
        var (trip, isClient, isPro) = await TripAccess.LoadAsync(db, cmd.Id, actor.UserId, ct);
        if (trip is null || !(isClient || isPro))
        {
            return Error.NotFound("trips.not_found", "Trip not found.");
        }

        if (!trip.TryCancel(clock.GetUtcNow()))
        {
            return Error.Conflict("trips.not_cancellable", $"A trip in '{trip.Status}' cannot be cancelled.");
        }

        await requests.ReopenAsync(trip.RequestId, ct);
        outbox.Enqueue(new TripCancelled(trip.Id, trip.RequestId, isClient ? "client" : "professional"));
        await audit.WriteAsync("trip.cancelled", "Trip", trip.Id, new { by = isClient ? "client" : "professional" }, ct: ct);

        return trip.ToDto();
    }
}
