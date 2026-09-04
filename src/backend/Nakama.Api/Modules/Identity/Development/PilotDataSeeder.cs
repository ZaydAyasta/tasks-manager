using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Notifications.Domain;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;

namespace Nakama.Api.Modules.Identity.Development;

public sealed class PilotDataOptions
{
    public const string SectionName = "DevelopmentBootstrap:PilotData";

    public bool Enabled { get; init; }
    public bool AdoptExistingProject { get; init; }
    public string? AdminPassword { get; init; }
    public string? CollaboratorPassword { get; init; }
    public string? NonMemberPassword { get; init; }
}

public static class PilotDataSeeder
{
    public const string ProjectName = "Implementación Portal de Clientes";

    private const string DatasetDescription = "Dataset reproducible para piloto UAT de Nakama.";
    private static readonly PilotUser Ana = new("Ana Torres", "ana.torres@pilot.local", UserRole.Admin);
    private static readonly PilotUser Diego = new("Diego Ramos", "diego.ramos@pilot.local", UserRole.Collaborator);
    private static readonly PilotUser Lucia = new("Lucía Vargas", "lucia.vargas@pilot.local", UserRole.Collaborator);
    private static readonly string[] StageNames = ["Análisis", "Diseño", "Desarrollo", "Pruebas", "Entrega"];

    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var options = app.Configuration.GetSection(PilotDataOptions.SectionName).Get<PilotDataOptions>() ?? new PilotDataOptions();
        if (!options.Enabled)
        {
            return;
        }

        ValidateOptions(options);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await SeedAsync(dbContext, clock, passwordHasher, options);
    }

    public static async Task SeedAsync(
        NakamaDbContext dbContext,
        IClock clock,
        IPasswordHasher<User> passwordHasher,
        PilotDataOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var project = await dbContext.Projects.SingleOrDefaultAsync(project => project.Name == ProjectName, cancellationToken);
        if (project is not null && project.Status != ProjectStatus.Active)
        {
            throw new InvalidOperationException($"The existing project '{ProjectName}' must be Active for the pilot UAT dataset.");
        }
        if (project is not null && !options.AdoptExistingProject && project.Description != DatasetDescription)
        {
            throw new InvalidOperationException($"The existing project '{ProjectName}' is not the managed pilot UAT dataset. Set DevelopmentBootstrap:PilotData:AdoptExistingProject=true only after confirming it is the intended Development project.");
        }

        var ana = await EnsureUserAsync(dbContext, clock, passwordHasher, Ana, options.AdminPassword!, cancellationToken);
        var diego = await EnsureUserAsync(dbContext, clock, passwordHasher, Diego, options.CollaboratorPassword!, cancellationToken);
        await EnsureUserAsync(dbContext, clock, passwordHasher, Lucia, options.NonMemberPassword!, cancellationToken);

        if (project is null)
        {
            project = Project.Create(ProjectName, DatasetDescription, null, null, ana.Id, clock);
            dbContext.Projects.Add(project);
        }

        await EnsureProjectMemberAsync(dbContext, project, ana, ProjectRole.Owner, clock, cancellationToken);
        await EnsureProjectMemberAsync(dbContext, project, diego, ProjectRole.Member, clock, cancellationToken);

        var stages = new Dictionary<string, Stage>(StringComparer.Ordinal);
        for (var index = 0; index < StageNames.Length; index++)
        {
            var name = StageNames[index];
            var stage = await dbContext.Stages.SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.Name == name, cancellationToken);
            if (stage is null)
            {
                stage = Stage.Create(project.Id, name, "Etapa del dataset UAT.", index + 1, clock);
                dbContext.Stages.Add(stage);
            }
            else if (stage.Position != index + 1 || !stage.IsActive)
            {
                throw new InvalidOperationException($"The existing pilot stage '{name}' does not match the expected UAT dataset.");
            }

            stages.Add(name, stage);
        }

        var collaborativeFlow = await EnsureTaskAsync(dbContext, project, stages["Análisis"], ana, diego, "Validar flujo colaborativo", TaskPriority.High, clock, cancellationToken);
        var dependencyReview = await EnsureTaskAsync(dbContext, project, stages["Diseño"], ana, diego, "Revisar dependencia funcional", TaskPriority.Medium, clock, cancellationToken);
        await EnsureTaskAsync(dbContext, project, stages["Desarrollo"], ana, diego, "Implementar permisos de proyecto", TaskPriority.High, clock, cancellationToken);
        await EnsureTaskAsync(dbContext, project, stages["Pruebas"], ana, diego, "Ejecutar pruebas de aceptación", TaskPriority.Medium, clock, cancellationToken);
        await EnsureTaskAsync(dbContext, project, stages["Entrega"], ana, diego, "Preparar entrega del piloto", TaskPriority.Low, clock, cancellationToken);

        if (!await dbContext.TaskDependencies.AnyAsync(dependency => dependency.TaskId == dependencyReview.Id && dependency.DependsOnTaskId == collaborativeFlow.Id, cancellationToken))
        {
            dbContext.TaskDependencies.Add(TaskDependency.Create(dependencyReview.Id, collaborativeFlow.Id, ana.Id, clock));
        }

        if (!await dbContext.Notifications.AnyAsync(notification => notification.RecipientUserId == diego.Id
            && notification.TaskId == collaborativeFlow.Id
            && notification.Type == NotificationType.TaskAssigned, cancellationToken))
        {
            dbContext.Notifications.Add(Notification.Create(
                diego.Id,
                ana.Id,
                NotificationType.TaskAssigned,
                project.Id,
                collaborativeFlow.Id,
                "{\"source\":\"pilot-uat\"}",
                clock));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateOptions(PilotDataOptions options)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("DevelopmentBootstrap:PilotData:Enabled must be true when seeding pilot data.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminPassword)
            || string.IsNullOrWhiteSpace(options.CollaboratorPassword)
            || string.IsNullOrWhiteSpace(options.NonMemberPassword))
        {
            throw new InvalidOperationException("DevelopmentBootstrap:PilotData passwords must be supplied through secure Development configuration.");
        }
    }

    private static async Task<User> EnsureUserAsync(
        NakamaDbContext dbContext,
        IClock clock,
        IPasswordHasher<User> passwordHasher,
        PilotUser expected,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == expected.Email, cancellationToken);
        if (user is not null)
        {
            if (user.FullName != expected.FullName || user.Role != expected.Role || !user.IsActive)
            {
                throw new InvalidOperationException($"The existing pilot user '{expected.Email}' does not match the expected UAT identity.");
            }

            return user;
        }

        var profile = User.Create(expected.FullName, expected.Email, expected.Role, "pending", clock);
        user = User.Create(expected.FullName, expected.Email, expected.Role, passwordHasher.HashPassword(profile, password), clock);
        dbContext.Users.Add(user);
        return user;
    }

    private static async Task EnsureProjectMemberAsync(
        NakamaDbContext dbContext,
        Project project,
        User user,
        ProjectRole role,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers.SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.UserId == user.Id, cancellationToken);
        if (member is null)
        {
            dbContext.ProjectMembers.Add(ProjectMember.Create(project.Id, user.Id, role, clock));
        }
        else if (member.Role != role)
        {
            throw new InvalidOperationException($"The existing pilot member '{user.Email}' has an unexpected project role.");
        }
    }

    private static async Task<WorkTask> EnsureTaskAsync(
        NakamaDbContext dbContext,
        Project project,
        Stage stage,
        User creator,
        User assignee,
        string title,
        TaskPriority priority,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.Title == title, cancellationToken);
        if (task is null)
        {
            task = WorkTask.Create(project.Id, stage.Id, title, "Tarea preparada para el piloto UAT.", priority, null, creator.Id, clock);
            dbContext.Tasks.Add(task);
        }
        else if (task.StageId != stage.Id || task.Priority != priority)
        {
            throw new InvalidOperationException($"The existing pilot task '{title}' does not match the expected UAT dataset.");
        }

        if (!await dbContext.TaskAssignees.AnyAsync(item => item.TaskId == task.Id && item.UserId == assignee.Id, cancellationToken))
        {
            dbContext.TaskAssignees.Add(TaskAssignee.Create(task.Id, assignee.Id, clock));
        }

        return task;
    }

    private sealed record PilotUser(string FullName, string Email, UserRole Role);
}
