using Nakama.Api.BuildingBlocks.Time;
using System.Text.RegularExpressions;

namespace Nakama.Api.Modules.Identity.Domain;

public sealed partial class User
{
    private const int FullNameMaxLength = 150;
    private const int EmailMaxLength = 320;

    private User()
    {
    }

    private User(Guid id, string fullName, string email, UserRole role, DateTimeOffset createdAt)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(string fullName, string email, UserRole role, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureValidRole(role);

        var now = clock.UtcNow;
        return new User(Guid.NewGuid(), NormalizeFullName(fullName), NormalizeEmail(email), role, now);
    }

    public void UpdateProfile(string fullName, string email, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        FullName = NormalizeFullName(fullName);
        Email = NormalizeEmail(email);
        UpdatedAt = clock.UtcNow;
    }

    public void ChangeRole(UserRole role, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureValidRole(role);

        Role = role;
        UpdatedAt = clock.UtcNow;
    }

    public void Activate(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        IsActive = true;
        UpdatedAt = clock.UtcNow;
    }

    public void Deactivate(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        IsActive = false;
        UpdatedAt = clock.UtcNow;
    }

    public static bool TryParseRole(string? value, out UserRole role) =>
        Enum.TryParse(value, ignoreCase: false, out role) && Enum.IsDefined(role);

    private static string NormalizeFullName(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > FullNameMaxLength)
        {
            throw new ArgumentException("Full name is required and must not exceed 150 characters.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeEmail(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > EmailMaxLength || !EmailPattern().IsMatch(normalized))
        {
            throw new ArgumentException("Email is required and must have a valid format.", nameof(value));
        }

        return normalized;
    }

    private static void EnsureValidRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "The role is invalid.");
        }
    }

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")]
    private static partial Regex EmailPattern();
}
