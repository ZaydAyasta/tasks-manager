using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

internal static class UserAdministration
{
    public static async Task<IResult> ChangeAsync(Guid id, UserRole? role, bool deactivate,
        NakamaDbContext db, IClock clock, ICurrentUser actor, CancellationToken ct)
    {
        if (id == actor.UserId) return Results.Problem(title: "Otro administrador debe cambiar tu rol o desactivar tu cuenta.", statusCode: 409);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize changes to active administrators so concurrent requests cannot remove the last one.
        var admins = await db.Users.FromSqlRaw("SELECT * FROM users WHERE role = 'Admin' AND is_active ORDER BY id FOR UPDATE").ToListAsync(ct);
        if (!admins.Any(admin => admin.Id == actor.UserId)) return Results.Forbid();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return IdentityEndpointHelpers.NotFoundProblem(id);
        if (user.IsActive && user.Role == UserRole.Admin && (deactivate || role == UserRole.Collaborator) && admins.Count <= 1)
            return Results.Problem(title: "Debe quedar al menos un administrador activo.", statusCode: 409);
        if (deactivate) user.Deactivate(clock);
        else if (role is { } nextRole) user.ChangeRole(nextRole, clock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(title: "La cuenta cambió. Actualiza e inténtalo de nuevo.", statusCode: 409);
        }
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }
}
