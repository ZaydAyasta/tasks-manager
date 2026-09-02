using System.Text.Json;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Activity;

public sealed class ActivityRecorder(NakamaDbContext dbContext, IClock clock, ICurrentUser currentUser) : IActivityRecorder
{
    public void Record(Guid projectId, Guid? taskId, ActivityType type, object? metadata = null)
    {
        var metadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata);
        dbContext.ActivityLogs.Add(ActivityLog.Create(projectId, taskId, currentUser.UserId, type, metadataJson, clock));
    }

    public void Record(Guid projectId, Guid? taskId, Guid ignoredActorUserId, ActivityType type, object? metadata = null) => Record(projectId, taskId, type, metadata);
}
