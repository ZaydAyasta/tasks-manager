using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Notifications.Domain;

public enum NotificationType
{
    TaskAssigned, TaskUnassigned, TaskSubmittedForReview, TaskChangesRequested,
    TaskCompleted, BlockerReported, BlockerResolved, CommentAdded
}

public sealed class Notification
{
    private Notification() { }
    private Notification(Guid recipient, Guid? actor, NotificationType type, Guid? projectId, Guid? taskId, string? metadata, IClock clock)
    {
        Id = Guid.NewGuid(); RecipientUserId = recipient; ActorUserId = actor; Type = type;
        ProjectId = projectId; TaskId = taskId; MetadataJson = metadata; CreatedAt = clock.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public bool IsRead => ReadAt is not null;
    public static Notification Create(Guid recipient, Guid? actor, NotificationType type, Guid? projectId, Guid? taskId, string? metadata, IClock clock) => new(recipient, actor, type, projectId, taskId, metadata, clock);
    public void MarkRead(IClock clock) => ReadAt ??= clock.UtcNow;
}

public sealed class NotificationPreference
{
    private NotificationPreference() { }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public bool IsEnabled { get; private set; }
    public static NotificationPreference Create(Guid userId, NotificationType type, bool enabled) => new() { UserId = userId, Type = type, IsEnabled = enabled };
    public void Set(bool enabled) => IsEnabled = enabled;
}
