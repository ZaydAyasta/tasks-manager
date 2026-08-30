using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Tasks.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

public sealed class TaskBlockerDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Id = Guid.NewGuid();
    [Theory]
    [InlineData(DomainTaskStatus.Pending)]
    [InlineData(DomainTaskStatus.InProgress)]
    [InlineData(DomainTaskStatus.InReview)]
    public void Block_preserves_and_restores_allowed_state(DomainTaskStatus status)
    {
        var task = CreateAt(status); task.Block(new Clock(Now));
        Assert.Equal(DomainTaskStatus.Blocked, task.Status); Assert.Equal(status, task.StatusBeforeBlocked);
        task.ResolveBlocker(false, new Clock(Now.AddMinutes(1)));
        Assert.Equal(status, task.Status); Assert.Null(task.StatusBeforeBlocked);
    }
    [Fact]
    public void Additional_blocker_preserves_original_state_and_cancel_remains_terminal()
    {
        var task = CreateAt(DomainTaskStatus.InProgress); task.Block(new Clock(Now)); task.Block(new Clock(Now.AddMinutes(1)));
        Assert.Equal(DomainTaskStatus.InProgress, task.StatusBeforeBlocked);
        task.Cancel(new Clock(Now.AddMinutes(2))); task.ResolveBlocker(false, new Clock(Now.AddMinutes(3)));
        Assert.Equal(DomainTaskStatus.Cancelled, task.Status); Assert.Null(task.StatusBeforeBlocked);
    }
    [Fact]
    public void Terminal_tasks_cannot_be_blocked()
    {
        var completed = CreateAt(DomainTaskStatus.InReview); completed.Complete(new Clock(Now));
        Assert.Throws<InvalidOperationException>(() => completed.Block(new Clock(Now)));
        var cancelled = CreateAt(DomainTaskStatus.Pending); cancelled.Cancel(new Clock(Now));
        Assert.Throws<InvalidOperationException>(() => cancelled.Block(new Clock(Now)));
    }
    [Fact]
    public void Blocker_normalizes_and_cannot_be_resolved_twice()
    {
        var blocker = TaskBlocker.Report(Id, TaskBlockerType.Approval, "  Need approval  ", Id, new Clock(Now));
        blocker.Resolve(Id, new Clock(Now.AddMinutes(1)));
        Assert.Equal("Need approval", blocker.Description); Assert.True(blocker.IsResolved); Assert.Throws<InvalidOperationException>(() => blocker.Resolve(Id, new Clock(Now)));
    }
    private static WorkTask CreateAt(DomainTaskStatus status)
    {
        var task = WorkTask.Create(Id, Id, "Task", null, TaskPriority.Medium, null, Id, new Clock(Now));
        if (status is DomainTaskStatus.InProgress or DomainTaskStatus.InReview) task.Start(new Clock(Now));
        if (status is DomainTaskStatus.InReview) task.SubmitForReview(new Clock(Now));
        return task;
    }
    private sealed class Clock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
