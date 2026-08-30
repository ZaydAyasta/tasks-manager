using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ListProjectMembers
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapGet("/{projectId:guid}/members", HandleAsync);

    private static async Task<IResult> HandleAsync(Guid projectId, NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return ProjectEndpointHelpers.ProjectNotFound(projectId);
        }

        var members = await (from member in dbContext.ProjectMembers.AsNoTracking()
                             join user in dbContext.Users.AsNoTracking() on member.UserId equals user.Id
                             where member.ProjectId == projectId
                             orderby member.JoinedAt
                             select new ProjectMemberResponse(member.UserId, user.FullName, user.Email, member.Role.ToString(), member.JoinedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }
}
