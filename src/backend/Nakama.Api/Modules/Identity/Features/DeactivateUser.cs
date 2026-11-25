using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Identity.Features;

internal static class DeactivateUser
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/deactivate", HandleAsync);

    private static Task<IResult> HandleAsync(Guid id, NakamaDbContext dbContext, IClock clock, ICurrentUser actor, CancellationToken cancellationToken) =>
        UserAdministration.ChangeAsync(id, null, true, dbContext, clock, actor, cancellationToken);
}
