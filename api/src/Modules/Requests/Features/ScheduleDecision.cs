using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests.Features;

public sealed record ScheduleDecisionCommand(Guid RequestId, string Decision, DateOnly? ScheduledDate)
    : ICommand<Result<TransportRequestDto>>;

public sealed class ScheduleDecisionValidator : AbstractValidator<ScheduleDecisionCommand>
{
    public ScheduleDecisionValidator()
    {
        RuleFor(x => x.Decision).Must(d => d is "schedule" or "decline")
            .WithMessage("decision must be 'schedule' or 'decline'.");
        RuleFor(x => x.ScheduledDate).NotNull().When(x => x.Decision == "schedule")
            .WithMessage("scheduledDate is required when scheduling.");
    }
}

public sealed class ScheduleDecisionHandler(
    DbContext db,
    ICurrentActor actor,
    IAppConfiguration config,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<ScheduleDecisionCommand, Result<TransportRequestDto>>
{
    public async Task<Result<TransportRequestDto>> Handle(ScheduleDecisionCommand cmd, CancellationToken ct)
    {
        var r = await db.Set<TransportRequest>().FirstOrDefaultAsync(x => x.Id == cmd.RequestId, ct);
        if (r is null || r.ClientId != actor.UserId)
        {
            return Error.NotFound("requests.not_found", "Request not found.");
        }

        if (r.Status != RequestStatus.AwaitingScheduleDecision)
        {
            return Error.Conflict("requests.not_awaiting_decision", "This request is not awaiting a schedule decision.");
        }

        var now = clock.GetUtcNow();

        if (cmd.Decision == "decline")
        {
            r.MarkUnfulfilled(now);
            outbox.Enqueue(new RequestStatusChanged(r.Id, "awaiting_schedule_decision", "unfulfilled"));
            await audit.WriteAsync("request.schedule_declined", "TransportRequest", r.Id, ct: ct);
            return r.ToDto();
        }

        var date = cmd.ScheduledDate!.Value;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var windowDays = await config.GetIntAsync(ConfigKeys.SchedulingWindowDays, 30, ct);
        if (date <= today || date > today.AddDays(windowDays))
        {
            return Error.Validation("requests.invalid_schedule_date",
                $"scheduledDate must be between tomorrow and {windowDays} days ahead.");
        }

        r.ChooseSchedule(date, now);
        outbox.Enqueue(new RequestStatusChanged(r.Id, "awaiting_schedule_decision", "scheduled_searching"));
        outbox.Enqueue(new RequestScheduleRequested(
            r.Id, r.ClientId, date, r.EstimatedWeightGrams, r.EstimatedPrice, r.Currency));
        await audit.WriteAsync("request.schedule_requested", "TransportRequest", r.Id, new { date }, ct: ct);

        return r.ToDto();
    }
}
