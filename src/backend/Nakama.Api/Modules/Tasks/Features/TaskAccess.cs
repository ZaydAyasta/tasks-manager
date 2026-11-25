using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Tasks.Features;

internal static class TaskAccess
{
    public static async Task<bool> CanWorkAsync(NakamaDbContext db, Domain.WorkTask task, ICurrentUser current, CancellationToken ct) =>
        await CurrentUserAccess.IsActiveAdminAsync(db, current, ct) ||
        await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == current.UserId, ct) &&
        await db.TaskAssignees.AnyAsync(x => x.TaskId == task.Id && x.UserId == current.UserId, ct);
}
