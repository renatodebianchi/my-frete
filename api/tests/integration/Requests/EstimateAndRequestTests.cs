using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Requests;

[Collection(ApiCollection.Name)]
public sealed class EstimateAndRequestTests(ApiFactory factory)
{
    // Paulista -> Ibirapuera, ~4 km apart
    private static readonly object Origin = new { text = "Av. Paulista, 1000", point = new { lat = -23.5613, lng = -46.6560 } };
    private static readonly object Destination = new { text = "Parque Ibirapuera", point = new { lat = -23.5874, lng = -46.6576 } };

    // T043 — POST /pricing/estimate

    [Fact]
    public async Task Estimate_returns_a_price_marked_as_estimate()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/v1/pricing/estimate", new
        {
            origin = Origin,
            destination = Destination,
            estimatedWeightKg = 40m,
        });

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("amount").GetDecimal().Should().BeGreaterThan(20m); // min_price
        body.GetProperty("isEstimate").GetBoolean().Should().BeTrue();
        body.GetProperty("distanceSource").GetString().Should().Be("geodesic_fallback"); // no external key in tests
        body.GetProperty("distanceKm").GetDouble().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Estimate_rejects_identical_origin_and_destination()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/v1/pricing/estimate", new
        {
            origin = Origin,
            destination = Origin,
            estimatedWeightKg = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // T044 — POST /requests, GET /requests, GET /requests/{id}, cancel

    [Fact]
    public async Task Create_request_then_read_back_shows_searching_status_and_estimate()
    {
        var client = await ClientAsync();

        var create = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "Caixa de livros", quantity = 2 } },
            estimatedWeightKg = 30m,
            origin = Origin,
            destination = Destination,
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var get = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{id}");
        get.GetProperty("status").GetString().Should().Be("searching");
        get.GetProperty("kind").GetString().Should().Be("immediate");
        get.GetProperty("estimate").GetProperty("amount").GetDecimal().Should().BeGreaterThan(0);
        get.GetProperty("items").GetArrayLength().Should().Be(1);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/requests");
        list.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Cancel_moves_the_request_to_cancelled()
    {
        var client = await ClientAsync();
        var create = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "Mala", quantity = 1 } },
            estimatedWeightKg = 15m,
            origin = Origin,
            destination = Destination,
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cancel = await client.PostAsync($"/v1/requests/{id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("cancelled");

        var cancelAgain = await client.PostAsync($"/v1/requests/{id}/cancel", null);
        cancelAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_professional_cannot_create_a_request()
    {
        var (token, _) = await factory.RegisterAsync($"pro-{Guid.NewGuid():N}@e.com", ["professional"], maxLoadKg: 100);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "x", quantity = 1 } },
            estimatedWeightKg = 10m,
            origin = Origin,
            destination = Destination,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> ClientAsync()
    {
        var (token, _) = await factory.RegisterAsync($"c-{Guid.NewGuid():N}@e.com", ["client"]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
