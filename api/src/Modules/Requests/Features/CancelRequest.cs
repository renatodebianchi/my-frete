using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests.Features;

public sealed record CancelRequestCommand(Guid Id) : ICommand<Result<TransportRequestDto>>;

public sealed class CancelRequestHandler(
    DbContext db,
    ICurrentActor actor,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<CancelRequestCommand, Result<TransportRequestDto>>
{
    public async Task<Result<TransportRequestDto>> Handle(CancelRequestCommand cmd, CancellationToken ct)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (r is null || r.ClientId != actor.UserId)
        {
            return Error.NotFound("requests.not_found", "Request not found.");
        }

        var previous = r.Status;
        if (!r.TryCancel(clock.GetUtcNow()))
        {
            return Error.Conflict("requests.not_cancellable", $"A request in '{previous.ToWire()}' cannot be cancelled.");
        }

        outbox.Enqueue(new RequestCancelled(r.Id, "client"));
        outbox.Enqueue(new RequestStatusChanged(r.Id, previous.ToWire(), "cancelled"));
        await audit.WriteAsync("request.cancelled", "TransportRequest", r.Id, new { from = previous.ToWire() }, ct: ct);

        return r.ToDto();
    }
}
