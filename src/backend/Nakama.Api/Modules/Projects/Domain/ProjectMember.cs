using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Projects.Domain;

public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    private ProjectMember(Guid projectId, Guid userId, ProjectRole role, DateTimeOffset joinedAt)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectRole role, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (projectId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId and UserId are required.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "The project role is invalid.");
        }

        return new ProjectMember(projectId, userId, role, clock.UtcNow);
    }
}
