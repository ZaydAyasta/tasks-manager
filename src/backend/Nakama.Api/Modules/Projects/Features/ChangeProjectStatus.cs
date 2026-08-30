using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ChangeProjectStatus
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/pause", (Guid id, ProjectVersionRequest request, NakamaDbContext dbContext, IClock clock, CancellationToken cancellationToken) =>
            HandleAsync(id, request, dbContext, clock, ProjectStatus.Paused, cancellationToken));
        group.MapPost("/{id:guid}/resume", (Guid id, ProjectVersionRequest request, NakamaDbContext dbContext, IClock clock, CancellationToken cancellationToken) =>
            HandleAsync(id, request, dbContext, clock, ProjectStatus.Active, cancellationToken));
        group.MapPost("/{id:guid}/complete", (Guid id, ProjectVersionRequest request, NakamaDbContext dbContext, IClock clock, CancellationToken cancellationToken) =>
            HandleAsync(id, request, dbContext, clock, ProjectStatus.Completed, cancellationToken));
        group.MapPost("/{id:guid}/cancel", (Guid id, ProjectVersionRequest request, NakamaDbContext dbContext, IClock clock, CancellationToken cancellationToken) =>
            HandleAsync(id, request, dbContext, clock, ProjectStatus.Cancelled, cancellationToken));
    }

    private static async Task<IResult> HandleAsync(Guid id, ProjectVersionRequest request, NakamaDbContext dbContext, IClock clock, ProjectStatus targetStatus, CancellationToken cancellationToken)
    {
        var project = await ProjectEndpointHelpers.FindProjectAsync(dbContext, id, cancellationToken);
        if (project is null)
        {
            return ProjectEndpointHelpers.ProjectNotFound(id);
        }

        if (!ProjectEndpointHelpers.HasExpectedVersion(project, request.Version))
        {
            return ProjectEndpointHelpers.VersionConflict();
        }

        try
        {
            switch (targetStatus)
            {
                case ProjectStatus.Paused:
                    project.Pause(clock);
                    break;
                case ProjectStatus.Active:
                    project.Resume(clock);
                    break;
                case ProjectStatus.Completed:
                    project.Complete(clock);
                    break;
                case ProjectStatus.Cancelled:
                    project.Cancel(clock);
                    break;
            }
        }
        catch (InvalidOperationException exception)
        {
            return ProjectEndpointHelpers.InvalidTransition(exception.Message);
        }

        ProjectEndpointHelpers.SetOriginalVersion(dbContext, project, request.Version!.Value);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ProjectEndpointHelpers.VersionConflict();
        }

        return Results.NoContent();
    }
}
