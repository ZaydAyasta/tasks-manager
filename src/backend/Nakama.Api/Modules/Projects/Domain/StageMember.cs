using Nakama.Api.BuildingBlocks.Time;
namespace Nakama.Api.Modules.Projects.Domain;
public sealed class StageMember
{
    private StageMember() { }
    private StageMember(Guid stageId, Guid userId, StageRole role, DateTimeOffset now) { Id = Guid.NewGuid(); StageId = stageId; UserId = userId; Role = role; AssignedAt = now; }
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public Guid UserId { get; private set; }
    public StageRole Role { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public static StageMember Create(Guid stageId, Guid userId, StageRole role, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (stageId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("StageId and UserId are required.");
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        return new StageMember(stageId, userId, role, clock.UtcNow);
    }
}
