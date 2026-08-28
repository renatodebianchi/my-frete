namespace MyFrete.Api.Cli;

/// <summary>
/// Scaffold for `dotnet run --project src/MyFrete.Api -- seed --demo`.
/// Seeds a demo <c>PricingRule</c> and the single service area used by the MVP.
/// Real implementation depends on the EF <c>DbContext</c> (T010) and the typed
/// configuration store (T015); wired here once those land.
/// </summary>
public static class SeedCommand
{
    public const string Verb = "seed";

    public static Task<int> RunAsync(string[] args, IServiceProvider services, CancellationToken ct)
    {
        var demo = args.Contains("--demo");
        Console.WriteLine(demo
            ? "[seed] --demo: pricing rule + service area seeding pending T010/T015."
            : "[seed] no-op: pass --demo to seed demo data.");
        return Task.FromResult(0);
    }
}
