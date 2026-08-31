using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests.Features;

public sealed record TransportRequestDto(
    Guid Id,
    string Status,
    string Kind,
    DateOnly? ScheduledDate,
    EstimateDto Estimate,
    IReadOnlyList<RequestItem> Items,
    string OriginAddress,
    string DestinationAddress,
    Guid? AssignedProfessionalId,
    DateTimeOffset CreatedAt);

public sealed record EstimateDto(decimal Amount, string Currency, double DistanceKm, string DistanceSource, bool IsEstimate);

public static class RequestMapping
{
    public static TransportRequestDto ToDto(this TransportRequest r) => new(
        r.Id,
        r.Status.ToWire(),
        r.Kind.ToString().ToLowerInvariant(),
        r.ScheduledDate,
        new EstimateDto(r.EstimatedPrice, r.Currency, Math.Round(r.DistanceMeters / 1000.0, 2), r.DistanceSource, true),
        r.Items,
        r.OriginAddress,
        r.DestinationAddress,
        r.AssignedProfessionalId,
        r.CreatedAt);

}

public sealed record GetRequestQuery(Guid Id) : IQuery<Result<TransportRequestDto>>;

public sealed class GetRequestHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<GetRequestQuery, Result<TransportRequestDto>>
{
    public async Task<Result<TransportRequestDto>> Handle(GetRequestQuery q, CancellationToken ct)
    {
        var r = await db.Set<TransportRequest>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (r is null || r.ClientId != actor.UserId)
        {
            return Error.NotFound("requests.not_found", "Request not found.");
        }

        return r.ToDto();
    }
}

public sealed record ListRequestsQuery(int Page) : IQuery<Result<IReadOnlyList<TransportRequestDto>>>;

public sealed class ListRequestsHandler(DbContext db, ICurrentActor actor)
    : IRequestHandler<ListRequestsQuery, Result<IReadOnlyList<TransportRequestDto>>>
{
    public async Task<Result<IReadOnlyList<TransportRequestDto>>> Handle(ListRequestsQuery q, CancellationToken ct)
    {
        if (actor.UserId is not { } clientId)
        {
            return Error.Unauthorized("requests.not_authenticated", "Not authenticated.");
        }

        var page = Math.Max(1, q.Page);
        var items = await db.Set<TransportRequest>().AsNoTracking()
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync(ct);

        return items.Select(r => r.ToDto()).ToList();
    }
}
