using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Provisioning;

public sealed record FirstAdminProvisioningResult(string Email, string TemporaryPassword);

public sealed class FirstAdminProvisioningException(string message) : Exception(message);

public sealed class FirstAdminProvisioner(
    NakamaDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IClock clock)
{
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*-_";
    private const int TemporaryPasswordLength = 28;

    public async Task<FirstAdminProvisioningResult> CreateAsync(
        string fullName,
        string email,
        CancellationToken cancellationToken = default)
    {
        User profile;
        try
        {
            profile = User.Create(fullName, email, UserRole.Admin, "validation-only", clock);
        }
        catch (ArgumentException exception)
        {
            throw new FirstAdminProvisioningException(exception.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        if (await dbContext.Users.AnyAsync(user => user.Email == profile.Email, cancellationToken))
        {
            throw new FirstAdminProvisioningException("A user with that email already exists. No changes were made.");
        }

        if (await dbContext.Users.AnyAsync(user => user.Role == UserRole.Admin && user.IsActive, cancellationToken))
        {
            throw new FirstAdminProvisioningException("An active Admin already exists. Use the application to manage additional users.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var admin = User.Create(profile.FullName, profile.Email, UserRole.Admin,
            passwordHasher.HashPassword(profile, temporaryPassword), clock);
        dbContext.Users.Add(admin);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new FirstAdminProvisioningException("The first Admin could not be created because the database changed concurrently. No changes were made.");
        }

        return new FirstAdminProvisioningResult(admin.Email, temporaryPassword);
    }

    public static string GenerateTemporaryPassword()
    {
        Span<char> password = stackalloc char[TemporaryPasswordLength];
        for (var index = 0; index < password.Length; index++)
        {
            password[index] = PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];
        }

        return new string(password);
    }
}
