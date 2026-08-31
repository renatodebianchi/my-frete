using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class ProfessionalRegistrationTests(ApiFactory factory)
{
    // T033 — contract: /auth/register (professional + maxLoadKg) and PATCH /professionals/me

    [Fact]
    public async Task Register_professional_requires_max_load()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            name = "Pedro Pro",
            email = $"pro-{Guid.NewGuid():N}@example.com",
            phone = "+5511933334444",
            password = "s3nhaForte!",
            roles = new[] { "professional" },
            // maxLoadKg omitted
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Register_professional_then_me_reports_capacity_and_verification()
    {
        var (token, _) = await factory.RegisterAsync($"pro-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 180);
        var client = Authed(token);

        var me = await client.GetFromJsonAsync<JsonElement>("/v1/accounts/me");
        var pro = me.GetProperty("professional");
        pro.GetProperty("maxLoadKg").GetDecimal().Should().Be(180m);
        pro.GetProperty("immediateAvailability").GetBoolean().Should().BeFalse();
        pro.GetProperty("verificationStatus").GetString().Should().Be("NaoVerificado");
    }

    [Fact]
    public async Task Patch_professional_me_updates_capacity_and_availability()
    {
        var (token, _) = await factory.RegisterAsync($"pro-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 100);
        var client = Authed(token);

        var patch = await client.PatchAsJsonAsync("/v1/professionals/me", new
        {
            maxLoadKg = 220m,
            immediateAvailability = true,
        });

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maxLoadKg").GetDecimal().Should().Be(220m);
        body.GetProperty("immediateAvailability").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Patch_professional_me_is_forbidden_for_a_client_only_account()
    {
        var (token, _) = await factory.RegisterAsync($"client-{Guid.NewGuid():N}@example.com", ["client"]);
        var client = Authed(token);

        var patch = await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });

        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Location_update_requires_being_available()
    {
        var (token, _) = await factory.RegisterAsync($"pro-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 100);
        var client = Authed(token);

        var beforeAvailable = await client.PatchAsJsonAsync("/v1/professionals/me/location", new { lat = -23.56, lng = -46.64 });
        beforeAvailable.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });

        var afterAvailable = await client.PatchAsJsonAsync("/v1/professionals/me/location", new { lat = -23.56, lng = -46.64 });
        afterAvailable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetFromJsonAsync<JsonElement>("/v1/accounts/me");
        me.GetProperty("professional").GetProperty("lastLocationAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
