using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Features;

internal static class RemoveProjectMember
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapDelete("/{projectId:guid}/members/{userId:guid}", HandleAsync);

    private static async Task<IResult> HandleAsync(Guid projectId, Guid userId, NakamaDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return ProjectEndpointHelpers.ProjectNotFound(projectId);
        }

        var member = await dbContext.ProjectMembers.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId, cancellationToken);
        if (member is null)
        {
            return ProjectEndpointHelpers.Problem("project-member-not-found", "El usuario no pertenece al proyecto.", StatusCodes.Status404NotFound);
        }

        if (member.Role == ProjectRole.Owner)
        {
            return ProjectEndpointHelpers.OwnerCannotBeRemoved();
        }

        if (await (from stageMember in dbContext.StageMembers
                   join stage in dbContext.Stages on stageMember.StageId equals stage.Id
                   where stage.ProjectId == projectId && stageMember.UserId == userId
                   select stageMember.Id).AnyAsync(cancellationToken))
        {
            return ProjectEndpointHelpers.Problem("project-member-still-assigned-to-stages", "El usuario sigue asignado a etapas del proyecto.", StatusCodes.Status409Conflict);
        }

        if (await dbContext.TaskAssignees.AnyAsync(assignee => assignee.UserId == userId && dbContext.Tasks.Any(task => task.Id == assignee.TaskId && task.ProjectId == projectId), cancellationToken))
        {
            return ProjectEndpointHelpers.Problem("project-member-still-assigned", "El usuario sigue asignado a tareas del proyecto.", StatusCodes.Status409Conflict);
        }

        dbContext.ProjectMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
