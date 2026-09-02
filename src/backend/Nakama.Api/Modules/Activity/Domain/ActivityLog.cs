using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Activity.Domain;

public sealed class ActivityLog
{
    private ActivityLog() { }

    private ActivityLog(Guid projectId, Guid? taskId, Guid actorUserId, ActivityType type, string? metadataJson, DateTimeOffset occurredAt)
    {
        if (projectId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Project and actor are required.");
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = Guid.NewGuid();
        ProjectId = projectId;
        TaskId = taskId;
        ActorUserId = actorUserId;
        ActivityType = type;
        MetadataJson = metadataJson;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public ActivityType ActivityType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? MetadataJson { get; private set; }

    public static ActivityLog Create(Guid projectId, Guid? taskId, Guid actorUserId, ActivityType type, string? metadataJson, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new ActivityLog(projectId, taskId, actorUserId, type, metadataJson, clock.UtcNow);
    }
}
