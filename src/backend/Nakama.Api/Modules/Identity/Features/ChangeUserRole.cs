using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;

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
        ICurrentUser actor,
        CancellationToken cancellationToken)
    {
        if (!IdentityEndpointHelpers.TryGetRole(request.Role, out var role))
        {
            return IdentityEndpointHelpers.ValidationProblem("Role debe ser Admin o Collaborator.");
        }

        return await UserAdministration.ChangeAsync(id, role, false, dbContext, clock, actor, cancellationToken);
    }
}
