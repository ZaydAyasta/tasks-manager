using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Tasks.Domain;

public sealed class TaskComment
{
    private TaskComment() { }
    private TaskComment(Guid taskId, Guid authorUserId, string content, IClock clock)
    {
        if (taskId == Guid.Empty || authorUserId == Guid.Empty) throw new ArgumentException("Task and author are required.");
        Id = Guid.NewGuid(); TaskId = taskId; AuthorUserId = authorUserId; Content = Normalize(content); CreatedAt = clock.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsEdited => UpdatedAt is not null;
    public static TaskComment Create(Guid taskId, Guid authorUserId, string content, IClock clock) => new(taskId, authorUserId, content, clock);
    public void Edit(string content, IClock clock) { Content = Normalize(content); UpdatedAt = clock.UtcNow; }
    private static string Normalize(string? content) { var normalized = content?.Trim() ?? string.Empty; if (normalized.Length is < 1 or > 4000) throw new ArgumentException("Content must contain between 1 and 4000 characters."); return normalized; }
}
