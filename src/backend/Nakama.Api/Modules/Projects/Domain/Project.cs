using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Projects.Domain;

public sealed class Project
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    private Project()
    {
    }

    private Project(Guid id, string name, string? description, DateOnly? startDate, DateOnly? endDate, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        Status = ProjectStatus.Active;
        StartDate = startDate;
        EndDate = endDate;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public static Project Create(string name, string? description, DateOnly? startDate, DateOnly? endDate, Guid createdByUserId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        }

        ValidateDateRange(startDate, endDate);
        return new Project(Guid.NewGuid(), NormalizeName(name), NormalizeDescription(description), startDate, endDate, createdByUserId, clock.UtcNow);
    }

    public void UpdateDetails(string name, string? description, DateOnly? startDate, DateOnly? endDate, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ValidateDateRange(startDate, endDate);

        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        StartDate = startDate;
        EndDate = endDate;
        Touch(clock);
    }

    public void Pause(IClock clock) => ChangeStatus(ProjectStatus.Paused, clock);
    public void Resume(IClock clock) => ChangeStatus(ProjectStatus.Active, clock);
    public void Complete(IClock clock) => ChangeStatus(ProjectStatus.Completed, clock);
    public void Cancel(IClock clock) => ChangeStatus(ProjectStatus.Cancelled, clock);

    private void ChangeStatus(ProjectStatus nextStatus, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var transitionIsAllowed = (Status, nextStatus) switch
        {
            (ProjectStatus.Active, ProjectStatus.Paused) => true,
            (ProjectStatus.Paused, ProjectStatus.Active) => true,
            (ProjectStatus.Active or ProjectStatus.Paused, ProjectStatus.Completed or ProjectStatus.Cancelled) => true,
            _ => false
        };

        if (!transitionIsAllowed)
        {
            throw new InvalidOperationException($"Cannot transition a project from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
        Touch(clock);
    }

    private void Touch(IClock clock)
    {
        UpdatedAt = clock.UtcNow;
        Version++;
    }

    private static string NormalizeName(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > NameMaxLength)
        {
            throw new ArgumentException("Name is required and must not exceed 200 characters.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException("Description must not exceed 2000 characters.", nameof(value));
        }

        return normalized;
    }

    private static void ValidateDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate is not null && endDate is not null && startDate > endDate)
        {
            throw new ArgumentException("StartDate must be earlier than or equal to EndDate.");
        }
    }
}
