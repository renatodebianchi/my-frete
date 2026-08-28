using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyFrete.Migrations;

public static class DependencyInjection
{
    /// <summary>Registers <see cref="AppDbContext"/> against PostgreSQL + PostGIS (NetTopologySuite).</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure();
            }).UseSnakeCaseNamingConvention());

        services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }

    /// <summary>Applies pending migrations. Called on startup when RunMigrationsOnStartup=true (Docker-first).</summary>
    public static async Task MigrateAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
