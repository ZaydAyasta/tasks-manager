using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record AuthUserResponse(Guid Id, string FullName, string Email, string Role, bool MustChangePassword = false);
public sealed record LoginResponse(string? AccessToken, DateTimeOffset ExpiresAt, AuthUserResponse User, string CsrfToken);
public sealed record MeResponse(Guid Id, string FullName, string Email, string Role, bool IsActive, string CsrfToken, bool MustChangePassword = false);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

internal static class AuthEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization(Policies.AuthenticatedUser);
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
        var email = User.NormalizeLogin(request.Email);
        if (!rateLimiter.TryAcquire(httpContext.Connection.RemoteIpAddress, email))
        {
            return Results.Problem(type: "https://nakama/errors/login-rate-limit", title: "Demasiados intentos de inicio de sesión. Inténtalo de nuevo más tarde.", statusCode: StatusCodes.Status429TooManyRequests);
        }

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
            new AuthUserResponse(user.Id, user.FullName, user.Email, user.Role.ToString(), user.MustChangePassword),
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
            currentUser.CsrfToken,
            user.MustChangePassword));
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, ICurrentUser currentUser,
        NakamaDbContext db, IPasswordHasher<User> hasher, IJwtTokenService tokens, BuildingBlocks.Time.IClock clock,
        ILoginRateLimiter rateLimiter, HttpContext context, IWebHostEnvironment environment, CancellationToken ct)
    {
        if (!rateLimiter.TryAcquire(context.Connection.RemoteIpAddress, $"password:{currentUser.UserId}"))
            return Results.Problem(title: "Demasiados intentos. Inténtalo más tarde.", statusCode: 429);
        var user = await db.Users.SingleAsync(x => x.Id == currentUser.UserId, ct);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Results.Problem(title: "La contraseña actual no es correcta.", statusCode: 400);
        if (!PasswordEndpoints.IsValidPassword(request.NewPassword) || request.NewPassword == request.CurrentPassword)
            return Results.Problem(title: "Usa una contraseña diferente, de entre 12 y 128 caracteres.", statusCode: 400);
        user.ChangePassword(hasher.HashPassword(user, request.NewPassword!), false, clock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(title: "La cuenta cambió. Vuelve a iniciar sesión.", statusCode: 409); }
        var token = tokens.Create(user);
        context.Response.Cookies.Append(AccessTokenCookieName, token.AccessToken, CookieOptions(environment, token.ExpiresAt));
        return Results.Ok(new LoginResponse(environment.IsDevelopment() ? token.AccessToken : null, token.ExpiresAt,
            new AuthUserResponse(user.Id, user.FullName, user.Email, user.Role.ToString(), user.MustChangePassword), token.CsrfToken));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, ICurrentUser currentUser,
        NakamaDbContext db, IClock clock, IWebHostEnvironment environment, CancellationToken ct)
    {
        if (!long.TryParse(httpContext.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var expiration))
            return Results.Unauthorized();

        // The signed CSRF proof is random and unique to each session, including JWTs issued before this migration.
        var fingerprint = RevokedSession.FingerprintOf(currentUser.CsrfToken);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiration);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH expired AS (DELETE FROM revoked_sessions WHERE expires_at <= {clock.UtcNow})
            INSERT INTO revoked_sessions (fingerprint, expires_at) VALUES ({fingerprint}, {expiresAt})
            ON CONFLICT (fingerprint) DO NOTHING
            """, ct);

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
