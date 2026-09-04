using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Projects.Features;

internal static class ProjectAccess
{
    public static async Task<bool> CanAccessAsync(NakamaDbContext dbContext, Guid projectId, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        return currentUser.Role == UserRole.Admin || await dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUser.UserId, cancellationToken);
    }
}
