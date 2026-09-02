using Nakama.Api.Modules.Activity.Domain;

namespace Nakama.Api.Modules.Activity;

public interface IActivityRecorder
{
    void Record(Guid projectId, Guid? taskId, ActivityType type, object? metadata = null);
    void Record(Guid projectId, Guid? taskId, Guid ignoredActorUserId, ActivityType type, object? metadata = null);
}
