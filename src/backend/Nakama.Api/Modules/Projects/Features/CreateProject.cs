using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Projects.Features;

internal static class CreateProject
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapPost("", HandleAsync).RequireAuthorization(Policies.Admin);

    private static async Task<IResult> HandleAsync(CreateProjectRequest request, NakamaDbContext dbContext, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var creatorId = currentUser.UserId;

        var creator = await ProjectEndpointHelpers.FindUserAsync(dbContext, creatorId, cancellationToken);
        if (creator is null)
        {
            return ProjectEndpointHelpers.UserNotFound(creatorId);
        }

        if (!creator.IsActive)
        {
            return ProjectEndpointHelpers.UserInactive();
        }

        Project project;
        try
        {
            project = Project.Create(request.Name ?? string.Empty, request.Description, request.StartDate, request.EndDate, creatorId, clock);
        }
        catch (ArgumentException exception)
        {
            return ProjectEndpointHelpers.Validation(exception.Message);
        }

        dbContext.Projects.Add(project);
        dbContext.ProjectMembers.Add(ProjectMember.Create(project.Id, creatorId, ProjectRole.Owner, clock));
        activities.Record(project.Id, null, ActivityType.ProjectCreated, new { project.Name });
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ProjectCreatedResponse(
            project.Id, project.Name, project.Description, project.Status.ToString(), project.StartDate, project.EndDate,
            new ProjectOwnerResponse(creator.Id, creator.FullName), project.CreatedAt, project.Version);

        return Results.Created($"/api/projects/{project.Id}", response);
    }
}
