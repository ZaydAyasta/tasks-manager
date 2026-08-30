using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;
using Npgsql;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ProjectEndpointHelpers
{
    public static IResult Problem(string type, string title, int status, string? detail = null) => Results.Problem(
        type: $"https://nakama/errors/{type}", title: title, detail: detail, statusCode: status);

    public static IResult Validation(string detail) => Problem("project-validation", "Los datos del proyecto no son válidos.", StatusCodes.Status400BadRequest, detail);
    public static IResult ProjectNotFound(Guid id) => Problem("project-not-found", "No existe el proyecto.", StatusCodes.Status404NotFound, $"No existe un proyecto con id '{id}'.");
    public static IResult UserNotFound(Guid id) => Problem("user-not-found", "No existe el usuario.", StatusCodes.Status404NotFound, $"No existe un usuario con id '{id}'.");
    public static IResult UserInactive() => Problem("project-user-inactive", "El usuario está inactivo.", StatusCodes.Status409Conflict);
    public static IResult DuplicateMember() => Problem("project-member-already-exists", "El usuario ya pertenece al proyecto.", StatusCodes.Status409Conflict);
    public static IResult OwnerCannotBeRemoved() => Problem("project-owner-cannot-be-removed", "El Owner no puede eliminarse del proyecto.", StatusCodes.Status409Conflict);
    public static IResult InvalidTransition(string detail) => Problem("project-invalid-status-transition", "La transición de estado no es válida.", StatusCodes.Status409Conflict, detail);
    public static IResult VersionConflict() => Problem("project-version-conflict", "El proyecto fue modificado por otro usuario.", StatusCodes.Status409Conflict);

    public static Task<Project?> FindProjectAsync(NakamaDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
        dbContext.Projects.SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public static Task<User?> FindUserAsync(NakamaDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public static bool HasExpectedVersion(Project project, int? version) => version is > 0 && project.Version == version;

    public static void SetOriginalVersion(NakamaDbContext dbContext, Project project, int version) =>
        dbContext.Entry(project).Property(item => item.Version).OriginalValue = version;

    public static bool IsMemberUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_project_members_project_user" };
}
