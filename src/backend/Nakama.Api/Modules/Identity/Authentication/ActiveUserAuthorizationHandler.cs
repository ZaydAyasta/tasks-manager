using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Authentication;

public sealed record ActiveUserRequirement(bool RequireAdmin) : IAuthorizationRequirement;

public sealed class ActiveUserAuthorizationHandler(NakamaDbContext dbContext) : AuthorizationHandler<ActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    {
        var subject = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId)) return;
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId);
        if (user is null || !user.IsActive || (requirement.RequireAdmin && user.Role != UserRole.Admin)) return;
        context.Succeed(requirement);
    }
}
