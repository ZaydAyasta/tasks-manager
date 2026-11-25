using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

public sealed record ResetPasswordRequest(string? TemporaryPassword);

internal static class PasswordEndpoints
{
    public static bool IsValidPassword(string? password) => password is { Length: >= 12 and <= 128 } && !string.IsNullOrWhiteSpace(password);

    public static void MapEndpoints(RouteGroupBuilder group) => group.MapPost("/{id:guid}/reset-password", ResetAsync);

    private static async Task<IResult> ResetAsync(Guid id, ResetPasswordRequest request, NakamaDbContext db,
        IPasswordHasher<User> hasher, IClock clock, ICurrentUser actor, CancellationToken ct)
    {
        if (id == actor.UserId) return Results.Problem(title: "Usa Mi cuenta para cambiar tu propia contraseña.", statusCode: 400);
        if (!IsValidPassword(request.TemporaryPassword)) return Results.Problem(title: "La contraseña temporal debe tener entre 12 y 128 caracteres.", statusCode: 400);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return IdentityEndpointHelpers.NotFoundProblem(id);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.TemporaryPassword!) != PasswordVerificationResult.Failed)
            return Results.Problem(title: "La contraseña temporal debe ser distinta de la anterior.", statusCode: 400);
        user.ChangePassword(hasher.HashPassword(user, request.TemporaryPassword!), true, clock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(title: "La cuenta cambió. Actualiza e inténtalo de nuevo.", statusCode: 409); }
        return Results.NoContent();
    }
}
