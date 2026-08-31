using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using MyFrete.Modules.Accounts.Auth;
using MyFrete.Modules.Accounts.Domain;
using Xunit;

namespace MyFrete.Tests.Unit;

public class PasswordAndTokenTests
{
    private static User NewUser() => new()
    {
        Name = "Ana",
        Email = "ana@example.com",
        Phone = "+5511999999999",
        Roles = [Roles.Client, Roles.Professional],
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void Password_hash_round_trips()
    {
        var hasher = new PasswordHasher<User>();
        var user = NewUser();
        var hash = hasher.HashPassword(user, "correct horse battery staple");

        hasher.VerifyHashedPassword(user, hash, "correct horse battery staple")
            .Should().Be(PasswordVerificationResult.Success);
        hasher.VerifyHashedPassword(user, hash, "wrong")
            .Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void Access_token_contains_sub_email_and_roles()
    {
        var options = new JwtOptions { SigningKey = new string('k', 48), AccessTokenMinutes = 15 };
        var service = new TokenService(options, TimeProvider.System);
        var user = NewUser();

        var issued = service.Issue(user);
        issued.ExpiresInSeconds.Should().BeInRange(60, 15 * 60);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);
        var types = jwt.Claims.Select(c => c.Type).ToList();
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        types.Should().Contain(t => t == "email" || t.EndsWith("/emailaddress", StringComparison.Ordinal));
        types.Count(t => t == "role" || t.EndsWith("/claims/role", StringComparison.Ordinal))
            .Should().Be(2);
    }

    [Fact]
    public void Refresh_token_hash_is_stable_and_opaque()
    {
        var service = new TokenService(new JwtOptions { SigningKey = new string('k', 48) }, TimeProvider.System);
        var hash1 = service.HashRefreshToken("token-abc");
        var hash2 = service.HashRefreshToken("token-abc");

        hash1.Should().Be(hash2).And.NotBe("token-abc");
        service.HashRefreshToken("token-xyz").Should().NotBe(hash1);
    }
}
