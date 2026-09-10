using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record AuthUserResponse(Guid Id, string FullName, string Email, string Role);
public sealed record LoginResponse(string? AccessToken, DateTimeOffset ExpiresAt, AuthUserResponse User, string CsrfToken);
public sealed record MeResponse(Guid Id, string FullName, string Email, string Role, bool IsActive, string CsrfToken);

internal static class AuthEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(Policies.AuthenticatedUser);
    }

    private const string AccessTokenCookieName = "nakama.access-token";

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        ILoginRateLimiter rateLimiter,
        NakamaDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService tokens,
        IWebHostEnvironment environment,
        CancellationToken ct)
    {
        if (!rateLimiter.TryAcquire(httpContext.Connection.RemoteIpAddress, request.Email))
        {
            return Results.Problem(type: "https://nakama/errors/login-rate-limit", title: "Demasiados intentos de inicio de sesión. Inténtalo de nuevo más tarde.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        var email = request.Email?.Trim().ToLowerInvariant();
        var user = string.IsNullOrEmpty(email) ? null : await db.Users.SingleOrDefaultAsync(item => item.Email == email, ct);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(request.Password) || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Results.Problem(type: "https://nakama/errors/invalid-credentials", title: "Credenciales inválidas.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = tokens.Create(user);
        httpContext.Response.Cookies.Append(AccessTokenCookieName, token.AccessToken, CookieOptions(environment, token.ExpiresAt));
        return Results.Ok(new LoginResponse(
            environment.IsDevelopment() ? token.AccessToken : null,
            token.ExpiresAt,
            new AuthUserResponse(user.Id, user.FullName, user.Email, user.Role.ToString()),
            token.CsrfToken));
    }

    private static async Task<IResult> MeAsync(
        ICurrentUser currentUser,
        NakamaDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentUser.UserId, ct);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new MeResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            currentUser.CsrfToken));
    }

    private static IResult LogoutAsync(HttpContext httpContext, IWebHostEnvironment environment)
    {
        httpContext.Response.Cookies.Delete(AccessTokenCookieName, CookieOptions(environment, DateTimeOffset.UnixEpoch));
        return Results.NoContent();
    }

    private static CookieOptions CookieOptions(IWebHostEnvironment environment, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Path = "/api",
        Expires = expiresAt
    };
}
