using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;

namespace MyFrete.BuildingBlocks.Behaviors;

/// <summary>
/// Commits a single transaction per command: the handler's changes plus any outbox rows it
/// enqueued are saved atomically (Constitution §VI). Queries are passed through untouched.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(DbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not (ICommand or ICommand<TResponse>))
        {
            return await next();
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return response;
        });
    }
}
