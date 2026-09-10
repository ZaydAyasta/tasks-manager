using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Authentication;

internal static class CurrentUserAccess
{
    public static Task<bool> IsActiveAdminAsync(NakamaDbContext dbContext, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == currentUser.UserId && user.IsActive && user.Role == UserRole.Admin, cancellationToken);
}
