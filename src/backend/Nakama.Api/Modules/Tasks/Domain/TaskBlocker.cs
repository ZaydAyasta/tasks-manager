using Nakama.Api.BuildingBlocks.Time;
namespace Nakama.Api.Modules.Tasks.Domain;
public sealed class TaskBlocker
{
    private TaskBlocker() { }
    private TaskBlocker(Guid taskId, TaskBlockerType type, string description, Guid reporter, DateTimeOffset reportedAt) { if (taskId == Guid.Empty || reporter == Guid.Empty) throw new ArgumentException("Task and reporter are required."); if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type)); Id = Guid.NewGuid(); TaskId = taskId; Type = type; Description = Normalize(description); ReportedByUserId = reporter; ReportedAt = reportedAt; }
    public Guid Id { get; private set; } public Guid TaskId { get; private set; } public TaskBlockerType Type { get; private set; } public string Description { get; private set; } = null!; public Guid ReportedByUserId { get; private set; } public DateTimeOffset ReportedAt { get; private set; } public Guid? ResolvedByUserId { get; private set; } public DateTimeOffset? ResolvedAt { get; private set; } public bool IsResolved => ResolvedAt is not null;
    public static TaskBlocker Report(Guid taskId, TaskBlockerType type, string description, Guid reporter, IClock clock) { ArgumentNullException.ThrowIfNull(clock); return new(taskId, type, description, reporter, clock.UtcNow); }
    public void Resolve(Guid resolver, IClock clock) { ArgumentNullException.ThrowIfNull(clock); if (resolver == Guid.Empty) throw new ArgumentException("Resolver is required."); if (IsResolved) throw new InvalidOperationException("Blocker is already resolved."); ResolvedByUserId = resolver; ResolvedAt = clock.UtcNow; }
    private static string Normalize(string description) { var normalized = description?.Trim() ?? string.Empty; if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1000) throw new ArgumentException("Description is required and must not exceed 1000 characters."); return normalized; }
}
