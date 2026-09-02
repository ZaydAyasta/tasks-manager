using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity.Domain;
using Xunit;

namespace Nakama.Api.Tests.Activity;

public sealed class ActivityLogTests
{
    [Fact]
    public void Create_sets_immutable_audit_fields()
    {
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var activity = ActivityLog.Create(projectId, taskId, actorId, ActivityType.TaskCreated, "{\"stageId\":\"value\"}", new FixedClock(now));

        Assert.Equal(projectId, activity.ProjectId);
        Assert.Equal(taskId, activity.TaskId);
        Assert.Equal(actorId, activity.ActorUserId);
        Assert.Equal(ActivityType.TaskCreated, activity.ActivityType);
        Assert.Equal(now, activity.OccurredAt);
        Assert.Equal("{\"stageId\":\"value\"}", activity.MetadataJson);
    }

    [Fact]
    public void Create_rejects_missing_human_actor()
    {
        Assert.Throws<ArgumentException>(() => ActivityLog.Create(Guid.NewGuid(), null, Guid.Empty, ActivityType.ProjectCreated, null, new FixedClock(DateTimeOffset.UtcNow)));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
