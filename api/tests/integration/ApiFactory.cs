using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace MyFrete.Tests.Integration;

/// <summary>
/// Boots the real API against throwaway PostGIS + Redis containers. Migrations are applied on
/// startup, exactly as in production (Docker-first).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("myfrete")
        .WithUsername("myfrete")
        .WithPassword("myfrete")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .WithCommand("--notify-keyspace-events", "Ex")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("RunMigrationsOnStartup", "true");
        builder.UseSetting("Otlp:Endpoint", string.Empty);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        // Force the host (and startup migration) to build now.
        using var client = CreateClient();
        (await client.GetAsync("/ready")).EnsureSuccessStatusCode();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    public async Task<(string AccessToken, string RefreshToken)> RegisterAsync(
        string email,
        IEnumerable<string> roles,
        decimal? maxLoadKg = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            name = "Test User",
            email,
            phone = "+5511900000000",
            password = "s3nhaForte!",
            roles,
            maxLoadKg,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenBody>();
        return (body!.AccessToken, body.RefreshToken);
    }

    private sealed record TokenBody(string AccessToken, string RefreshToken, int ExpiresInSeconds);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
