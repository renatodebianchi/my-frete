using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class ClientRegistrationTests(ApiFactory factory)
{
    // T026 — contract: /auth/register (client) + GET /accounts/me

    [Fact]
    public async Task Register_client_returns_tokens_and_me_reports_the_client_role()
    {
        var client = factory.CreateClient();
        var email = $"client-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            name = "Carla Cliente",
            email,
            phone = "+5511911112222",
            password = "s3nhaForte!",
            roles = new[] { "client" },
        });

        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = await register.Content.ReadFromJsonAsync<TokenBody>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.GetFromJsonAsync<MeBody>("/v1/accounts/me");

        me!.Email.Should().Be(email);
        me.Roles.Should().ContainSingle().Which.Should().Be("client");
        me.Professional.Should().BeNull();
    }

    [Fact]
    public async Task Register_with_duplicate_email_is_conflict()
    {
        var client = factory.CreateClient();
        var email = $"dupe-{Guid.NewGuid():N}@example.com";
        var body = new
        {
            name = "Dupe",
            email,
            phone = "+5511900000000",
            password = "s3nhaForte!",
            roles = new[] { "client" },
        };

        (await client.PostAsJsonAsync("/v1/auth/register", body)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/v1/auth/register", body);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_registration_body_is_unprocessable_entity_with_field_errors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            name = "X",
            email = "not-an-email",
            phone = "",
            password = "123",
            roles = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        problem!.Errors.Should().ContainKey("Email");
    }

    private sealed record TokenBody(string AccessToken, string RefreshToken, int ExpiresInSeconds);

    private sealed record MeBody(string Email, string[] Roles, object? Professional);

    private sealed record ProblemBody(Dictionary<string, string[]> Errors);
}
