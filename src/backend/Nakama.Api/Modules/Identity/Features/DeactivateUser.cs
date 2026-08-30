using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Identity.Features;

internal static class DeactivateUser
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/deactivate", HandleAsync);

    private static async Task<IResult> HandleAsync(Guid id, NakamaDbContext dbContext, IClock clock, CancellationToken cancellationToken)
    {
        var user = await IdentityEndpointHelpers.FindUserAsync(dbContext, id, cancellationToken);
        if (user is null)
        {
            return IdentityEndpointHelpers.NotFoundProblem(id);
        }

        user.Deactivate(clock);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
