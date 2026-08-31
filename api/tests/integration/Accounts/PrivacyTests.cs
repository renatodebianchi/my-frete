using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class PrivacyTests(ApiFactory factory)
{
    // T032a / T032b — LGPD data-subject request + self-service export (FR-030).

    [Fact]
    public async Task Data_subject_request_is_accepted_and_recorded()
    {
        var (accessToken, _) = await factory.RegisterAsync($"lgpd-{Guid.NewGuid():N}@example.com", ["client"]);
        var client = Authed(accessToken);

        var response = await client.PostAsJsonAsync("/v1/privacy/data-subject-requests",
            new { kind = "access", details = "please export my data" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Invalid_kind_is_unprocessable_entity()
    {
        var (accessToken, _) = await factory.RegisterAsync($"lgpd2-{Guid.NewGuid():N}@example.com", ["client"]);
        var client = Authed(accessToken);

        var response = await client.PostAsJsonAsync("/v1/privacy/data-subject-requests", new { kind = "sell-it" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Export_returns_only_the_callers_own_data()
    {
        var email = $"export-{Guid.NewGuid():N}@example.com";
        var (accessToken, _) = await factory.RegisterAsync(email, ["client", "professional"], maxLoadKg: 250);
        var client = Authed(accessToken);

        await client.PostAsJsonAsync("/v1/privacy/data-subject-requests", new { kind = "access" });

        var export = await client.GetFromJsonAsync<JsonElement>("/v1/privacy/me/export");

        export.GetProperty("account").GetProperty("email").GetString().Should().Be(email);
        export.GetProperty("professional").GetProperty("maxLoadKg").GetDecimal().Should().Be(250m);
        export.GetProperty("privacyRequests").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Export_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/v1/privacy/me/export");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rectification_updates_name_and_phone()
    {
        var email = $"rect-{Guid.NewGuid():N}@example.com";
        var (accessToken, _) = await factory.RegisterAsync(email, ["client"]);
        var client = Authed(accessToken);

        var patch = await client.PatchAsJsonAsync("/v1/accounts/me", new { name = "Nome Corrigido", phone = "+5511987654321" });

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await patch.Content.ReadFromJsonAsync<JsonElement>();
        me.GetProperty("name").GetString().Should().Be("Nome Corrigido");
        me.GetProperty("phone").GetString().Should().Be("+5511987654321");
    }

    [Fact]
    public async Task Deletion_anonymises_the_account_and_ends_the_session()
    {
        var email = $"del-{Guid.NewGuid():N}@example.com";
        var (accessToken, refreshToken) = await factory.RegisterAsync(email, ["client"]);
        var client = Authed(accessToken);

        var deletion = await client.PostAsJsonAsync("/v1/privacy/data-subject-requests", new { kind = "deletion" });
        deletion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // The refresh token is revoked.
        var refresh = await factory.CreateClient()
            .PostAsJsonAsync("/v1/auth/refresh", new { refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The access token still validates until it expires, but the profile is anonymised.
        var me = await client.GetFromJsonAsync<JsonElement>("/v1/accounts/me");
        me.GetProperty("name").GetString().Should().Be("Usuário removido");
        me.GetProperty("email").GetString().Should().NotBe(email);
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
