using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Auth;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Features;

public sealed record AuthResult(string AccessToken, string RefreshToken, int ExpiresInSeconds);

// ---------------------------------------------------------------- Register

public sealed record RegisterCommand(
    string Name,
    string Email,
    string Phone,
    string Password,
    IReadOnlyList<string> Roles,
    decimal? MaxLoadKg) : ICommand<Result<AuthResult>>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles).Must(r => r is Domain.Roles.Client or Domain.Roles.Professional)
            .WithMessage("Role must be 'client' or 'professional'.");
        RuleFor(x => x.MaxLoadKg).NotNull().GreaterThan(0)
            .When(x => x.Roles.Contains(Domain.Roles.Professional))
            .WithMessage("maxLoadKg is required for professionals.");
    }
}

public sealed class RegisterHandler(
    DbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    TimeProvider clock)
    : IRequestHandler<RegisterCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        if (await db.Set<User>().AnyAsync(u => u.Email == email, ct))
        {
            return Error.Conflict("accounts.email_taken", "That e-mail is already registered.");
        }

        var now = clock.GetUtcNow();
        var user = new User
        {
            Name = cmd.Name.Trim(),
            Email = email,
            Phone = cmd.Phone.Trim(),
            Roles = cmd.Roles.Distinct().ToList(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = hasher.HashPassword(user, cmd.Password);

        db.Set<User>().Add(user);

        if (user.HasRole(Domain.Roles.Professional))
        {
            db.Set<ProfessionalProfile>().Add(ProfessionalProfile.Create(user.Id, cmd.MaxLoadKg!.Value, now));
        }

        if (user.HasRole(Domain.Roles.Client))
        {
            db.Set<ClientProfile>().Add(new ClientProfile { UserId = user.Id });
        }

        var issued = tokens.Issue(user);
        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(issued.RefreshToken),
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
        });

        return new AuthResult(issued.AccessToken, issued.RefreshToken, issued.ExpiresInSeconds);
    }
}

// ---------------------------------------------------------------- Login

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<AuthResult>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler(
    DbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    TimeProvider clock)
    : IRequestHandler<LoginCommand, Result<AuthResult>>
{
    private const int MaxFailedAccess = 5;

    public async Task<Result<AuthResult>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);
        var now = clock.GetUtcNow();

        var invalid = Error.Unauthorized("accounts.invalid_credentials", "Invalid e-mail or password.");
        if (user is null)
        {
            return invalid;
        }

        if (user.IsLockedOut(now))
        {
            return Error.Unauthorized("accounts.locked_out", "Too many attempts. Try again later.");
        }

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, cmd.Password) == PasswordVerificationResult.Failed)
        {
            user.FailedAccessCount++;
            if (user.FailedAccessCount >= MaxFailedAccess)
            {
                user.LockoutEndsAt = now.AddMinutes(15);
                user.FailedAccessCount = 0;
            }

            return invalid;
        }

        user.FailedAccessCount = 0;
        user.LockoutEndsAt = null;

        var issued = tokens.Issue(user);
        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(issued.RefreshToken),
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
        });

        return new AuthResult(issued.AccessToken, issued.RefreshToken, issued.ExpiresInSeconds);
    }
}

// ---------------------------------------------------------------- Refresh (rotation)

public sealed record RefreshCommand(string RefreshToken) : ICommand<Result<AuthResult>>;

public sealed class RefreshHandler(DbContext db, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<RefreshCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(cmd.RefreshToken);
        var now = clock.GetUtcNow();

        var token = await db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || !token.IsActive(now))
        {
            return Error.Unauthorized("accounts.invalid_refresh_token", "Refresh token is invalid or expired.");
        }

        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Error.Unauthorized("accounts.invalid_refresh_token", "Refresh token is invalid or expired.");
        }

        var issued = tokens.Issue(user);
        var newHash = tokens.HashRefreshToken(issued.RefreshToken);

        token.RevokedAt = now;
        token.ReplacedByTokenHash = newHash;
        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
        });

        return new AuthResult(issued.AccessToken, issued.RefreshToken, issued.ExpiresInSeconds);
    }
}
