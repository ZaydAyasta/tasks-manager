using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ListProjects
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapGet("", HandleAsync);

    private static async Task<IResult> HandleAsync(string? status, NakamaDbContext dbContext, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        ProjectStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse(status, ignoreCase: false, out ProjectStatus candidate) || !Enum.IsDefined(candidate))
            {
                return ProjectEndpointHelpers.Validation("Status debe ser Active, Paused, Completed o Cancelled.");
            }

            parsedStatus = candidate;
        }

        var query = dbContext.Projects.AsNoTracking();
        if (currentUser.Role != UserRole.Admin)
            query = query.Where(project => dbContext.ProjectMembers.Any(member => member.ProjectId == project.Id && member.UserId == currentUser.UserId));
        if (parsedStatus is { } projectStatus)
        {
            query = query.Where(project => project.Status == projectStatus);
        }

        var projects = await query
            .OrderBy(project => project.Name)
            .Select(project => new ProjectListItemResponse(
                project.Id,
                project.Name,
                project.Status.ToString(),
                project.StartDate,
                project.EndDate,
                dbContext.ProjectMembers.Count(member => member.ProjectId == project.Id)))
            .ToListAsync(cancellationToken);

        return Results.Ok(projects);
    }
}
