using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Development;

public static class DevelopmentAdminSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var section = app.Configuration.GetSection("DevelopmentBootstrap:Admin");
        var email = section["Email"];
        var password = section["Password"];
        var fullName = section["FullName"] ?? "Development Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();

        if (await dbContext.Users.AnyAsync(user => user.Role == UserRole.Admin))
            return;

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var profile = User.Create(fullName, email, UserRole.Admin, "pending", clock);
        var admin = User.Create(fullName, email, UserRole.Admin, passwordHasher.HashPassword(profile, password), clock);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
    }
}
