using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

internal static class CreateUser
{
    public static void MapEndpoint(RouteGroupBuilder group) =>
        group.MapPost("", HandleAsync);

    private static async Task<IResult> HandleAsync(
        CreateUserRequest request,
        NakamaDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!IdentityEndpointHelpers.TryGetRole(request.Role, out var role))
        {
            return IdentityEndpointHelpers.ValidationProblem("Role debe ser Admin o Collaborator.");
        }

        User user;
        try
        {
            user = User.Create(request.FullName ?? string.Empty, request.Email ?? string.Empty, role, clock);
        }
        catch (ArgumentException exception)
        {
            return IdentityEndpointHelpers.ValidationProblem(exception.Message);
        }

        if (await dbContext.Users.AnyAsync(existing => existing.Email == user.Email, cancellationToken))
        {
            return IdentityEndpointHelpers.DuplicateEmailProblem();
        }

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IdentityEndpointHelpers.IsEmailUniqueViolation(exception))
        {
            return IdentityEndpointHelpers.DuplicateEmailProblem();
        }

        return Results.Created($"/api/users/{user.Id}", user.ToDetailResponse());
    }
}
