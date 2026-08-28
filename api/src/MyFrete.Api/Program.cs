using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (structured JSON; full OTel wiring in Phase 2 / T016) ---
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CLI entrypoint for `dotnet run -- seed --demo` (scaffold; real logic in T009 follow-up).
if (args.Length > 0 && args[0] == "seed")
{
    Console.WriteLine("[seed] scaffold — pricing rule + service area seeding lands with T009/T015.");
    return;
}

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health/readiness placeholders — real checks (Postgres + Redis) in Phase 2 / T022.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/ping", () => Results.Ok(new { pong = true }));

app.Run();

public partial class Program;
