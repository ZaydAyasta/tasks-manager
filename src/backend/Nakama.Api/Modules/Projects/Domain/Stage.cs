using Nakama.Api.BuildingBlocks.Time;

namespace Nakama.Api.Modules.Projects.Domain;

public sealed class Stage
{
    private Stage() { }
    private Stage(Guid projectId, string name, string? description, int position, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); ProjectId = projectId; SetDetails(name, description); Position = position;
        IsActive = true; Version = 1; CreatedAt = now; UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NameNormalized { get; private set; } = null!;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public bool IsActive { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static Stage Create(Guid projectId, string name, string? description, int position, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (projectId == Guid.Empty || position < 1) throw new ArgumentException("ProjectId and a positive position are required.");
        return new Stage(projectId, name, description, position, clock.UtcNow);
    }
    public void UpdateDetails(string name, string? description, IClock clock) { SetDetails(name, description); Touch(clock); }
    public void Activate(IClock clock) { if (IsActive) throw new InvalidOperationException("Stage is already active."); IsActive = true; Touch(clock); }
    public void Deactivate(IClock clock) { if (!IsActive) throw new InvalidOperationException("Stage is already inactive."); IsActive = false; Touch(clock); }
    public void Reposition(int position, IClock clock) { if (position < 1) throw new ArgumentOutOfRangeException(nameof(position)); Position = position; Touch(clock); }
    private void SetDetails(string name, string? description)
    {
        Name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200) throw new ArgumentException("Name is required and must not exceed 200 characters.");
        NameNormalized = Name.ToLowerInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > 2000) throw new ArgumentException("Description must not exceed 2000 characters.");
    }
    private void Touch(IClock clock) { ArgumentNullException.ThrowIfNull(clock); UpdatedAt = clock.UtcNow; Version++; }
}
