using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Projects.Features;

internal static class AddProjectMember
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapPost("/{projectId:guid}/members", HandleAsync).RequireAuthorization(Policies.Admin);

    private static async Task<IResult> HandleAsync(Guid projectId, AddProjectMemberRequest request, NakamaDbContext dbContext, IClock clock, IActivityRecorder activities, CancellationToken cancellationToken)
    {
        var project = await ProjectEndpointHelpers.FindProjectAsync(dbContext, projectId, cancellationToken);
        if (project is null)
        {
            return ProjectEndpointHelpers.ProjectNotFound(projectId);
        }

        if (request.UserId is not { } userId || userId == Guid.Empty)
        {
            return ProjectEndpointHelpers.Validation("UserId es obligatorio.");
        }

        var user = await ProjectEndpointHelpers.FindUserAsync(dbContext, userId, cancellationToken);
        if (user is null)
        {
            return ProjectEndpointHelpers.UserNotFound(userId);
        }

        if (!user.IsActive)
        {
            return ProjectEndpointHelpers.UserInactive();
        }

        if (await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId, cancellationToken))
        {
            return ProjectEndpointHelpers.DuplicateMember();
        }

        var member = ProjectMember.Create(projectId, userId, ProjectRole.Member, clock);
        dbContext.ProjectMembers.Add(member);
        activities.Record(projectId, null, ActivityType.ProjectMemberAdded, new { memberUserId = userId });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (ProjectEndpointHelpers.IsMemberUniqueViolation(exception))
        {
            return ProjectEndpointHelpers.DuplicateMember();
        }

        return Results.Created($"/api/projects/{projectId}/members/{userId}", new ProjectMemberResponse(userId, user.FullName, user.Email, member.Role.ToString(), member.JoinedAt));
    }
}
