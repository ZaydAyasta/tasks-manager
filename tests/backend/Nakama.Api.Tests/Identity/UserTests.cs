using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;
using Xunit;

namespace Nakama.Api.Tests.Identity;

public sealed class UserTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_full_name()
    {
        var user = User.Create("  Sofía Paredes  ", "sofia@nakama.com", UserRole.Collaborator, new FixedClock(InitialTime));

        Assert.Equal("Sofía Paredes", user.FullName);
    }

    [Fact]
    public void Create_normalizes_email()
    {
        var user = User.Create("Sofía Paredes", "  Sofia@Nakama.COM  ", UserRole.Collaborator, new FixedClock(InitialTime));

        Assert.Equal("sofia@nakama.com", user.Email);
    }

    [Fact]
    public void New_user_is_active()
    {
        var user = User.Create("Sofía Paredes", "sofia@nakama.com", UserRole.Collaborator, new FixedClock(InitialTime));

        Assert.True(user.IsActive);
        Assert.Equal(InitialTime, user.CreatedAt);
        Assert.Equal(InitialTime, user.UpdatedAt);
    }

    [Fact]
    public void Deactivate_deactivates_user()
    {
        var user = User.Create("Sofía Paredes", "sofia@nakama.com", UserRole.Collaborator, new FixedClock(InitialTime));
        var updatedAt = InitialTime.AddMinutes(1);

        user.Deactivate(new FixedClock(updatedAt));

        Assert.False(user.IsActive);
        Assert.Equal(updatedAt, user.UpdatedAt);
    }

    [Fact]
    public void Activate_reactivates_user()
    {
        var user = User.Create("Sofía Paredes", "sofia@nakama.com", UserRole.Collaborator, new FixedClock(InitialTime));
        user.Deactivate(new FixedClock(InitialTime.AddMinutes(1)));

        user.Activate(new FixedClock(InitialTime.AddMinutes(2)));

        Assert.True(user.IsActive);
        Assert.Equal(InitialTime.AddMinutes(2), user.UpdatedAt);
    }

    [Fact]
    public void Change_role_changes_role()
    {
        var user = User.Create("Sofía Paredes", "sofia@nakama.com", UserRole.Collaborator, new FixedClock(InitialTime));

        user.ChangeRole(UserRole.Admin, new FixedClock(InitialTime.AddMinutes(1)));

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Theory]
    [InlineData("", "sofia@nakama.com")]
    [InlineData("Sofía Paredes", "")]
    [InlineData("Sofía Paredes", "not-an-email")]
    public void Create_rejects_invalid_profile_data(string fullName, string email)
    {
        Assert.Throws<ArgumentException>(() => User.Create(fullName, email, UserRole.Collaborator, new FixedClock(InitialTime)));
    }

    [Fact]
    public void Create_rejects_invalid_role()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            User.Create("Sofía Paredes", "sofia@nakama.com", (UserRole)99, new FixedClock(InitialTime)));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
