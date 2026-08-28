using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Behaviors;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Redis;
using StackExchange.Redis;

namespace MyFrete.BuildingBlocks;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the shared application spine: MediatR + pipeline behaviors, FluentValidation,
    /// configuration store, audit log, transactional outbox (+ dispatcher), Redis helpers.
    /// </summary>
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        string redisConnectionString,
        params Assembly[] applicationAssemblies)
    {
        var assemblies = applicationAssemblies
            .Append(typeof(DependencyInjection).Assembly)
            .Distinct()
            .ToArray();

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
        });

        foreach (var assembly in assemblies)
        {
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        }

        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));
        services.TryAddSingleton<IRedisConnection, RedisConnection>();

        services.TryAddScoped<IAppConfiguration, AppConfiguration>();
        services.TryAddScoped<IAuditLog, AuditLog>();
        services.TryAddScoped<IOutboxWriter, OutboxWriter>();
        services.TryAddScoped<ICurrentActor, SystemActor>();

        services.AddHostedService<OutboxDispatcher>();

        return services;
    }
}
