using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Auth;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "my-frete";

    public string Audience { get; init; } = "my-frete-app";

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;
}

public sealed record IssuedTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public interface ITokenService
{
    IssuedTokens Issue(User user);

    string HashRefreshToken(string refreshToken);
}

public sealed class TokenService(JwtOptions options, TimeProvider clock) : ITokenService
{
    public IssuedTokens Issue(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return new IssuedTokens(accessToken, refreshToken, (int)(expires - now).TotalSeconds);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
