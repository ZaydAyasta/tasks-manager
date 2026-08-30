using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ListUserProjects
{
    public static void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet("/api/users/{userId:guid}/projects", HandleAsync).WithTags("Projects");

    private static async Task<IResult> HandleAsync(Guid userId, NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return ProjectEndpointHelpers.UserNotFound(userId);
        }

        var projects = await (from member in dbContext.ProjectMembers.AsNoTracking()
                              join project in dbContext.Projects.AsNoTracking() on member.ProjectId equals project.Id
                              where member.UserId == userId
                              orderby project.Name
                              select new ProjectListItemResponse(
                                  project.Id,
                                  project.Name,
                                  project.Status.ToString(),
                                  project.StartDate,
                                  project.EndDate,
                                  dbContext.ProjectMembers.Count(otherMember => otherMember.ProjectId == project.Id)))
            .ToListAsync(cancellationToken);

        return Results.Ok(projects);
    }
}
