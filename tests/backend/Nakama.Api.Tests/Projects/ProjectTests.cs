using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;
using Xunit;

namespace Nakama.Api.Tests.Projects;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CreatorId = Guid.Parse("4E13A793-C289-486C-B20F-1DDC3B70E200");

    [Fact]
    public void Create_starts_active_and_trims_name()
    {
        var project = Project.Create("  Portal redesign  ", "  Description  ", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), CreatorId, new FixedClock(InitialTime));

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal("Portal redesign", project.Name);
        Assert.Equal("Description", project.Description);
        Assert.Equal(1, project.Version);
    }

    [Fact]
    public void Create_accepts_valid_date_range()
    {
        var project = Project.Create("Portal", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), CreatorId, new FixedClock(InitialTime));

        Assert.Equal(new DateOnly(2026, 9, 1), project.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 1), project.EndDate);
    }

    [Fact]
    public void Create_rejects_invalid_date_range()
    {
        Assert.Throws<ArgumentException>(() => Project.Create("Portal", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1), CreatorId, new FixedClock(InitialTime)));
    }

    [Fact]
    public void Active_project_can_pause_and_paused_project_can_resume()
    {
        var project = CreateProject();
        project.Pause(new FixedClock(InitialTime.AddMinutes(1)));

        project.Resume(new FixedClock(InitialTime.AddMinutes(2)));

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(3, project.Version);
    }

    [Fact]
    public void Active_project_can_complete()
    {
        var project = CreateProject();

        project.Complete(new FixedClock(InitialTime.AddMinutes(1)));

        Assert.Equal(ProjectStatus.Completed, project.Status);
    }

    [Fact]
    public void Completed_project_cannot_resume()
    {
        var project = CreateProject();
        project.Complete(new FixedClock(InitialTime.AddMinutes(1)));

        Assert.Throws<InvalidOperationException>(() => project.Resume(new FixedClock(InitialTime.AddMinutes(2))));
    }

    [Fact]
    public void Cancelled_project_cannot_be_reactivated()
    {
        var project = CreateProject();
        project.Cancel(new FixedClock(InitialTime.AddMinutes(1)));

        Assert.Throws<InvalidOperationException>(() => project.Resume(new FixedClock(InitialTime.AddMinutes(2))));
    }

    [Fact]
    public void Updating_details_changes_timestamp_and_version()
    {
        var project = CreateProject();
        var changedAt = InitialTime.AddMinutes(1);

        project.UpdateDetails("Updated", null, null, null, new FixedClock(changedAt));

        Assert.Equal(changedAt, project.UpdatedAt);
        Assert.Equal(2, project.Version);
    }

    private static Project CreateProject() => Project.Create("Portal", null, null, null, CreatorId, new FixedClock(InitialTime));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
