using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;

namespace Nakama.Api.Modules.Identity.Features;

internal static class ListUsers
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapGet("", HandleAsync);

    private static async Task<IResult> HandleAsync(NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => user.ToListItemResponse())
            .ToListAsync(cancellationToken);

        return Results.Ok(users);
    }
}
