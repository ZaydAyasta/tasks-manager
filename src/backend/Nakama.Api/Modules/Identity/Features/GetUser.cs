using Nakama.Api.BuildingBlocks.Persistence;

namespace Nakama.Api.Modules.Identity.Features;

internal static class GetUser
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", HandleAsync);

    private static async Task<IResult> HandleAsync(Guid id, NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        var user = await IdentityEndpointHelpers.FindUserAsync(dbContext, id, cancellationToken);
        return user is null ? IdentityEndpointHelpers.NotFoundProblem(id) : Results.Ok(user.ToDetailResponse());
    }
}
