using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Features;

internal static class GetProject
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapGet("/{id:guid}", HandleAsync);

    private static async Task<IResult> HandleAsync(Guid id, NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null)
        {
            return ProjectEndpointHelpers.ProjectNotFound(id);
        }

        var owner = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == project.CreatedByUserId)
            .Select(user => new ProjectOwnerResponse(user.Id, user.FullName))
            .SingleAsync(cancellationToken);

        var members = await (from member in dbContext.ProjectMembers.AsNoTracking()
                             join user in dbContext.Users.AsNoTracking() on member.UserId equals user.Id
                             where member.ProjectId == id
                             orderby member.JoinedAt
                             select new ProjectMemberResponse(member.UserId, user.FullName, user.Email, member.Role.ToString(), member.JoinedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ProjectDetailResponse(
            project.Id, project.Name, project.Description, project.Status.ToString(), project.StartDate, project.EndDate,
            owner, members, project.CreatedAt, project.UpdatedAt, project.Version));
    }
}
