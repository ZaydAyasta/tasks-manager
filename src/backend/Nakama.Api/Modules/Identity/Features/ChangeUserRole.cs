using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Identity.Features;

internal static class ChangeUserRole
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}/role", HandleAsync);

    private static async Task<IResult> HandleAsync(
        Guid id,
        ChangeUserRoleRequest request,
        NakamaDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!IdentityEndpointHelpers.TryGetRole(request.Role, out var role))
        {
            return IdentityEndpointHelpers.ValidationProblem("Role debe ser Admin o Collaborator.");
        }

        var user = await IdentityEndpointHelpers.FindUserAsync(dbContext, id, cancellationToken);
        if (user is null)
        {
            return IdentityEndpointHelpers.NotFoundProblem(id);
        }

        user.ChangeRole(role, clock);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
