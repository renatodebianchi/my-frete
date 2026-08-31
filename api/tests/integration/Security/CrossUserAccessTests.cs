using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Security;

[Collection(ApiCollection.Name)]
public sealed class CrossUserAccessTests(ApiFactory factory)
{
    private static readonly object Origin = new { text = "P", point = new { lat = -23.56, lng = -46.65 } };
    private static readonly object Destination = new { text = "I", point = new { lat = -23.58, lng = -46.66 } };

    // T098 — a user must never reach another user's resources.

    [Fact]
    public async Task A_client_cannot_read_or_cancel_another_clients_request()
    {
        var alice = await ClientAsync();
        var bob = await ClientAsync();

        var create = await alice.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "x", quantity = 1 } },
            estimatedWeightKg = 10,
            origin = Origin,
            destination = Destination,
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await bob.GetAsync($"/v1/requests/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await bob.PostAsync($"/v1/requests/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_professional_cannot_touch_a_trip_they_are_not_on()
    {
        var stranger = await ProfessionalAsync();

        // A trip that does not involve the stranger.
        var response = await stranger.PostAsync($"/v1/trips/{Guid.NewGuid()}/deliver", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Offer_endpoints_reject_a_client()
    {
        var client = await ClientAsync();

        (await client.GetAsync("/v1/offers/inbox")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/v1/offers/{Guid.NewGuid()}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Professional_endpoints_reject_a_client_only_account()
    {
        var client = await ClientAsync();

        (await client.GetAsync("/v1/professionals/me/schedule-availability"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Every_write_endpoint_requires_a_token()
    {
        var anon = factory.CreateClient();

        (await anon.PostAsync("/v1/pricing/estimate", JsonContent.Create(new { }))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsync("/v1/requests", JsonContent.Create(new { }))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/v1/trips")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/v1/accounts/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> ClientAsync()
    {
        var (token, _) = await factory.RegisterAsync($"cu-{Guid.NewGuid():N}@e.com", ["client"]);
        return Authed(token);
    }

    private async Task<HttpClient> ProfessionalAsync()
    {
        var (token, _) = await factory.RegisterAsync($"pu-{Guid.NewGuid():N}@e.com", ["professional"], 100);
        return Authed(token);
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
