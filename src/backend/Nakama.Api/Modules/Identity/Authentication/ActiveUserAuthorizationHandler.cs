using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Authentication;

public sealed record ActiveUserRequirement(bool RequireAdmin) : IAuthorizationRequirement;

public sealed class ActiveUserAuthorizationHandler(NakamaDbContext dbContext, IHttpContextAccessor accessor) : AuthorizationHandler<ActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    {
        var subject = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId)) return;
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId);
        if (user is null || !user.IsActive || (requirement.RequireAdmin && user.Role != UserRole.Admin)) return;
        var version = context.User.FindFirst("security_version")?.Value ?? "0";
        if (!int.TryParse(version, out var tokenVersion) || tokenVersion != user.SecurityVersion) return;
        var path = accessor.HttpContext?.Request.Path.Value;
        if (user.MustChangePassword && path is not ("/api/auth/me" or "/api/auth/logout" or "/api/auth/change-password")) return;
        context.Succeed(requirement);
    }
}
