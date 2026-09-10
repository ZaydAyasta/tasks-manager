using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Identity.Provisioning;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class FirstAdminProvisionerTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task CreateAsync_creates_active_admin_with_normalized_email_and_hashed_password()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        await db.Users.ExecuteDeleteAsync();
        var provisioner = CreateProvisioner(scope.ServiceProvider);

        var result = await provisioner.CreateAsync("  Administrador  ", "  ADMIN@Nakama.Local ");
        var user = await db.Users.SingleAsync();

        Assert.Equal("admin@nakama.local", result.Email);
        Assert.Equal("Administrador", user.FullName);
        Assert.Equal("admin@nakama.local", user.Email);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.NotEqual(result.TemporaryPassword, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().VerifyHashedPassword(user, user.PasswordHash, result.TemporaryPassword));
    }

    [Fact]
    public void GenerateTemporaryPassword_returns_28_characters_from_the_safe_alphabet()
    {
        var password = FirstAdminProvisioner.GenerateTemporaryPassword();

        Assert.Equal(28, password.Length);
        Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*_-]+$", password);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_email_without_modifying_the_existing_user()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var original = await db.Users.SingleAsync();
        var provisioner = CreateProvisioner(scope.ServiceProvider);

        var exception = await Assert.ThrowsAsync<FirstAdminProvisioningException>(() =>
            provisioner.CreateAsync("Someone Else", original.Email));

        Assert.Contains("already exists", exception.Message);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(original.Id, (await db.Users.SingleAsync()).Id);
    }

    [Fact]
    public async Task CreateAsync_rejects_second_first_admin_without_writing_a_user()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var provisioner = CreateProvisioner(scope.ServiceProvider);

        var exception = await Assert.ThrowsAsync<FirstAdminProvisioningException>(() =>
            provisioner.CreateAsync("Another Admin", "another@nakama.local"));

        Assert.Contains("active Admin", exception.Message);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.DoesNotContain(await db.Users.ToListAsync(), user => user.Email == "another@nakama.local");
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_profile_without_writing_a_user()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        await db.Users.ExecuteDeleteAsync();
        var provisioner = CreateProvisioner(scope.ServiceProvider);

        await Assert.ThrowsAsync<FirstAdminProvisioningException>(() =>
            provisioner.CreateAsync("", "not-an-email"));

        Assert.Empty(await db.Users.ToListAsync());
    }

    private static FirstAdminProvisioner CreateProvisioner(IServiceProvider services) => new(
        services.GetRequiredService<NakamaDbContext>(),
        services.GetRequiredService<IPasswordHasher<User>>(),
        services.GetRequiredService<IClock>());
}
