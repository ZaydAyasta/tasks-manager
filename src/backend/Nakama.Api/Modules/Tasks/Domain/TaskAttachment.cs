using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Tasks.Domain;

public sealed class TaskAttachment
{
    private TaskAttachment() { }
    private TaskAttachment(Guid taskId, Guid uploadedByUserId, string originalFileName, string storedFileName, string contentType, long sizeBytes, IClock clock)
    {
        if (taskId == Guid.Empty || uploadedByUserId == Guid.Empty || sizeBytes <= 0) throw new ArgumentException("Task, uploader and a non-empty file are required.");
        Id = Guid.NewGuid(); TaskId = taskId; UploadedByUserId = uploadedByUserId; OriginalFileName = originalFileName; StoredFileName = storedFileName; ContentType = contentType; SizeBytes = sizeBytes; CreatedAt = clock.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string StoredFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public static TaskAttachment Create(Guid taskId, Guid uploader, string originalName, string storedName, string contentType, long size, IClock clock) => new(taskId, uploader, originalName, storedName, contentType, size, clock);
}
