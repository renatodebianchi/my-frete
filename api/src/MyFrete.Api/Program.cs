using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using MyFrete.Api.Cli;
using MyFrete.Api.Infrastructure;
using MyFrete.Api.Middleware;
using MyFrete.Api.Observability;
using MyFrete.BuildingBlocks.Redis;
using MyFrete.Migrations;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
builder.ConfigureSerilog();
builder.AddTelemetry();

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
var redisConnString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

builder.Services.AddPersistence(postgres);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnString));
builder.Services.AddSingleton<IRedisConnection, RedisConnection>();

builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgres", tags: ["ready"])
    .AddRedis(redisConnString, name: "redis", tags: ["ready"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var key = http.User.Identity?.IsAuthenticated == true
            ? $"user:{http.User.Identity!.Name}"
            : $"ip:{http.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddProblemDetailsHandling();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CLI: `dotnet MyFrete.Api.dll seed --demo`
if (args is ["seed", ..])
{
    using var seedApp = builder.Build();
    return await SeedCommand.RunAsync(args, seedApp.Services, CancellationToken.None);
}

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    Log.Information("Applying database migrations on startup");
    await app.Services.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health", new() { Predicate = _ => false });
app.MapHealthChecks("/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapGet("/v1/ping", () => Results.Ok(new { pong = true }));

app.Run();

return 0;

public partial class Program;
