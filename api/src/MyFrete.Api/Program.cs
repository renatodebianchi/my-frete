using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using MyFrete.Api.Auth;
using MyFrete.Api.Cli;
using MyFrete.Api.Infrastructure;
using MyFrete.Api.Middleware;
using MyFrete.Api.Observability;
using MyFrete.BuildingBlocks;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.Migrations;
using MyFrete.Modules.Accounts;
using MyFrete.Modules.Matching;
using MyFrete.Modules.Notifications;
using MyFrete.Modules.Pricing;
using MyFrete.Modules.Requests;
using MyFrete.Modules.Scheduling;
using MyFrete.Modules.Trips;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
builder.ConfigureSerilog();
builder.AddTelemetry();

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();

builder.Services.AddPersistence(postgres);
builder.Services.AddBuildingBlocks(
    redis,
    typeof(AccountsModule).Assembly,
    typeof(NotificationsModule).Assembly,
    typeof(PricingModule).Assembly,
    typeof(RequestsModule).Assembly,
    typeof(TripsModule).Assembly,
    typeof(MatchingModule).Assembly,
    typeof(SchedulingModule).Assembly);
builder.Services.AddAccountsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddPricingModule();
builder.Services.AddRequestsModule();
builder.Services.AddTripsModule();
builder.Services.AddMatchingModule();
builder.Services.AddSchedulingModule();

builder.Services.AddHealthChecks()
    .AddNpgSql(postgres, name: "postgres", tags: ["ready"])
    .AddRedis(redis, name: "redis", tags: ["ready"]);

var permitPerMinute = builder.Configuration.GetValue("RateLimiting:PermitPerMinute", 120);
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
            PermitLimit = permitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddProblemDetailsHandling();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.CustomSchemaIds(t => t.FullName!.Replace("+", ".")));

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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapGet("/v1/ping", () => Results.Ok(new { pong = true }));
app.MapAccountsEndpoints();
app.MapNotificationsEndpoints();
app.MapPricingEndpoints();
app.MapRequestsEndpoints();
app.MapMatchingEndpoints();
app.MapTripsEndpoints();
app.MapSchedulingEndpoints();

app.Run();

return 0;

public partial class Program;
