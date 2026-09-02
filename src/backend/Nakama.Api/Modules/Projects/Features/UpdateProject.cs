using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Projects.Features;

internal static class UpdateProject
{
    public static void MapEndpoint(RouteGroupBuilder group) => group.MapPut("/{id:guid}", HandleAsync).RequireAuthorization(Policies.Admin);

    private static async Task<IResult> HandleAsync(Guid id, UpdateProjectRequest request, NakamaDbContext dbContext, IClock clock, IActivityRecorder activities, CancellationToken cancellationToken)
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
            project.UpdateDetails(request.Name ?? string.Empty, request.Description, request.StartDate, request.EndDate, clock);
            activities.Record(project.Id, null, ActivityType.ProjectUpdated);
        }
        catch (ArgumentException exception)
        {
            return ProjectEndpointHelpers.Validation(exception.Message);
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
