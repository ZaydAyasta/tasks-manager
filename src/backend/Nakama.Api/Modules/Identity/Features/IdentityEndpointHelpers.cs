using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Errors;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Domain;
using Npgsql;

namespace Nakama.Api.Modules.Identity.Features;

internal static class IdentityEndpointHelpers
{
    public static IResult ValidationProblem(string detail) => BusinessProblemExtensions.Validation(detail).ToProblem();

    public static IResult NotFoundProblem(Guid id) => BusinessProblemExtensions.NotFound($"No existe un usuario con id '{id}'.").ToProblem();

    public static IResult DuplicateEmailProblem() => Results.Problem(
        type: "https://nakama/errors/user-email-already-exists",
        title: "Ya existe un usuario con ese correo.",
        statusCode: StatusCodes.Status409Conflict);

    public static bool TryGetRole(string? roleValue, out UserRole role) => User.TryParseRole(roleValue, out role);

    public static Task<User?> FindUserAsync(NakamaDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public static bool IsEmailUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_users_email" };
}
