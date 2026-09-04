using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Development;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Notifications.Domain;
using Nakama.Api.Modules.Tasks.Domain;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PilotDataSeederTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Production_does_not_run_the_pilot_data_seeder()
    {
        await using var app = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        }).Build();

        var exception = await Record.ExceptionAsync(() => PilotDataSeeder.SeedAsync(app));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Disabled_pilot_data_does_not_require_configuration_or_a_database_in_development()
    {
        await using var app = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        }).Build();

        var exception = await Record.ExceptionAsync(() => PilotDataSeeder.SeedAsync(app));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("AdminPassword")]
    [InlineData("CollaboratorPassword")]
    [InlineData("NonMemberPassword")]
    public async Task Enabled_pilot_data_requires_each_password_before_using_the_database(string missingPassword)
    {
        var options = new PilotDataOptions
        {
            Enabled = true,
            AdminPassword = missingPassword == "AdminPassword" ? null : "PilotAdminPassword1",
            CollaboratorPassword = missingPassword == "CollaboratorPassword" ? null : "PilotCollaboratorPassword1",
            NonMemberPassword = missingPassword == "NonMemberPassword" ? null : "PilotNonMemberPassword1"
        };
        var dbContext = new NakamaDbContext(new DbContextOptionsBuilder<NakamaDbContext>().Options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PilotDataSeeder.SeedAsync(dbContext, new SystemClock(), new PasswordHasher<User>(), options));

        Assert.Contains("PilotData passwords", exception.Message);
    }

    [PostgresFact]
    public async Task Pilot_data_creates_the_required_uat_dataset_without_duplicates_on_repeat()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var options = new PilotDataOptions
        {
            Enabled = true,
            AdminPassword = "PilotAdminPassword1",
            CollaboratorPassword = "PilotCollaboratorPassword1",
            NonMemberPassword = "PilotNonMemberPassword1"
        };

        await PilotDataSeeder.SeedAsync(dbContext, clock, passwordHasher, options);
        dbContext.ChangeTracker.Clear();

        var project = await dbContext.Projects.SingleAsync(item => item.Name == PilotDataSeeder.ProjectName);
        var users = await dbContext.Users
            .Where(item => item.Email.EndsWith("@pilot.local"))
            .OrderBy(item => item.Email)
            .ToListAsync();
        var stages = await dbContext.Stages
            .Where(item => item.ProjectId == project.Id)
            .OrderBy(item => item.Position)
            .Select(item => item.Name)
            .ToListAsync();
        var tasks = await dbContext.Tasks
            .Where(item => item.ProjectId == project.Id)
            .OrderBy(item => item.Title)
            .ToListAsync();
        var diego = users.Single(item => item.Email == "diego.ramos@pilot.local");
        var lucia = users.Single(item => item.Email == "lucia.vargas@pilot.local");
        var collaborativeFlow = tasks.Single(item => item.Title == "Validar flujo colaborativo");
        var dependencyReview = tasks.Single(item => item.Title == "Revisar dependencia funcional");

        Assert.Collection(users,
            user => { Assert.Equal("Ana Torres", user.FullName); Assert.Equal(UserRole.Admin, user.Role); },
            user => { Assert.Equal("Diego Ramos", user.FullName); Assert.Equal(UserRole.Collaborator, user.Role); },
            user => { Assert.Equal("Lucía Vargas", user.FullName); Assert.Equal(UserRole.Collaborator, user.Role); });
        Assert.Equal(new[] { "Análisis", "Diseño", "Desarrollo", "Pruebas", "Entrega" }, stages);
        Assert.Equal(5, tasks.Count);
        Assert.All(tasks, task => Assert.Equal(Nakama.Api.Modules.Tasks.Domain.TaskStatus.Pending, task.Status));
        Assert.True(await dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == project.Id && item.UserId == diego.Id));
        Assert.False(await dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == project.Id && item.UserId == lucia.Id));
        Assert.True(await dbContext.TaskAssignees.AnyAsync(item => item.TaskId == collaborativeFlow.Id && item.UserId == diego.Id));
        Assert.True(await dbContext.TaskDependencies.AnyAsync(item => item.TaskId == dependencyReview.Id && item.DependsOnTaskId == collaborativeFlow.Id));
        Assert.True(await dbContext.Notifications.AnyAsync(item => item.RecipientUserId == diego.Id && item.TaskId == collaborativeFlow.Id && item.Type == NotificationType.TaskAssigned));
        Assert.Equal(0, await dbContext.Subtasks.CountAsync());
        Assert.Equal(0, await dbContext.TaskBlockers.CountAsync());
        Assert.Equal(0, await dbContext.TaskComments.CountAsync());
        Assert.Equal(0, await dbContext.TaskAttachments.CountAsync());

        await PilotDataSeeder.SeedAsync(dbContext, clock, passwordHasher, options);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(3, await dbContext.Users.CountAsync(item => item.Email.EndsWith("@pilot.local")));
        Assert.Equal(5, await dbContext.Stages.CountAsync(item => item.ProjectId == project.Id));
        Assert.Equal(5, await dbContext.Tasks.CountAsync(item => item.ProjectId == project.Id));
        Assert.Equal(1, await dbContext.TaskDependencies.CountAsync(item => item.TaskId == dependencyReview.Id && item.DependsOnTaskId == collaborativeFlow.Id));
        Assert.Equal(1, await dbContext.Notifications.CountAsync(item => item.RecipientUserId == diego.Id && item.TaskId == collaborativeFlow.Id && item.Type == NotificationType.TaskAssigned));
    }

    [PostgresFact]
    public async Task Existing_project_requires_explicit_adoption_before_pilot_data_is_added()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var existingAdmin = await dbContext.Users.SingleAsync(item => item.Role == UserRole.Admin);
        dbContext.Projects.Add(Nakama.Api.Modules.Projects.Domain.Project.Create(
            PilotDataSeeder.ProjectName,
            "Development project awaiting explicit UAT adoption.",
            null,
            null,
            existingAdmin.Id,
            clock));
        await dbContext.SaveChangesAsync();

        var options = new PilotDataOptions
        {
            Enabled = true,
            AdminPassword = "PilotAdminPassword1",
            CollaboratorPassword = "PilotCollaboratorPassword1",
            NonMemberPassword = "PilotNonMemberPassword1"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PilotDataSeeder.SeedAsync(dbContext, clock, passwordHasher, options));

        Assert.Contains("AdoptExistingProject", exception.Message);
        Assert.Empty(await dbContext.Users.Where(item => item.Email.EndsWith("@pilot.local")).ToListAsync());

        var adoptionOptions = new PilotDataOptions
        {
            Enabled = true,
            AdoptExistingProject = true,
            AdminPassword = options.AdminPassword,
            CollaboratorPassword = options.CollaboratorPassword,
            NonMemberPassword = options.NonMemberPassword
        };
        await PilotDataSeeder.SeedAsync(dbContext, clock, passwordHasher, adoptionOptions);

        var ana = await dbContext.Users.SingleAsync(item => item.Email == "ana.torres@pilot.local");
        var pilotProject = await dbContext.Projects.SingleAsync(item => item.Name == PilotDataSeeder.ProjectName);
        Assert.True(await dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == pilotProject.Id && item.UserId == ana.Id));
    }
}
