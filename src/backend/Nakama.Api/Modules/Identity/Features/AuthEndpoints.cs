using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record AuthUserResponse(Guid Id, string FullName, string Email, string Role);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUserResponse User);
public sealed record MeResponse(Guid Id, string FullName, string Email, string Role, bool IsActive);

internal static class AuthEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", MeAsync).RequireAuthorization(Policies.AuthenticatedUser);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, NakamaDbContext db, IPasswordHasher<User> passwordHasher, IJwtTokenService tokens, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var user = string.IsNullOrEmpty(email) ? null : await db.Users.SingleOrDefaultAsync(item => item.Email == email, ct);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(request.Password) || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Results.Problem(type: "https://nakama/errors/invalid-credentials", title: "Credenciales inválidas.", statusCode: StatusCodes.Status401Unauthorized);
        var token = tokens.Create(user);
        return Results.Ok(new LoginResponse(token.AccessToken, token.ExpiresAt, new AuthUserResponse(user.Id, user.FullName, user.Email, user.Role.ToString())));
    }

    private static async Task<IResult> MeAsync(ICurrentUser currentUser, NakamaDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentUser.UserId, ct);
        return user is null ? Results.Unauthorized() : Results.Ok(new MeResponse(user.Id, user.FullName, user.Email, user.Role.ToString(), user.IsActive));
    }
}
