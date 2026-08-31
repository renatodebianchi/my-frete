using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class AuthGateTests(ApiFactory factory)
{
    // T027 — FR-003: creating a transport request requires authentication.

    [Fact]
    public async Task Anonymous_request_creation_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/v1/requests", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_client_gets_past_the_auth_gate()
    {
        var (accessToken, _) = await factory.RegisterAsync(
            $"gate-{Guid.NewGuid():N}@example.com", ["client"]);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync("/v1/requests", content: null);

        // 501 = past the gate, endpoint body lands in US1. The point is: not 401.
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Professional_only_user_cannot_create_a_request()
    {
        var (accessToken, _) = await factory.RegisterAsync(
            $"proonly-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 100);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync("/v1/requests", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
