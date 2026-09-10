using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Notifications.Domain;
using Nakama.Api.Modules.Projects.Features;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record CreateTaskRequest(Guid? StageId, string? Title, string? Description, string? Priority, DateTimeOffset? DueDate, IReadOnlyList<Guid>? AssigneeIds);
public sealed record UpdateTaskRequest(string? Title, string? Description, string? Priority, DateTimeOffset? DueDate, long? Version);
public sealed record MoveTaskRequest(Guid? StageId, long? Version);
public sealed record VersionRequest(long? Version);
public sealed record AddAssigneeRequest(Guid? UserId, long? Version);
public sealed record AssigneeResponse(Guid Id, string FullName, string Email);
public sealed record SubtaskProgressResponse(int Completed, int Total);
public sealed record DependencyProgressResponse(int Satisfied, int Total);
public sealed record TaskResponse(Guid Id, Guid ProjectId, Guid StageId, string Title, string? Description, string Status, string Priority, DateTimeOffset? DueDate, IReadOnlyList<AssigneeResponse> Assignees, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt, SubtaskProgressResponse SubtaskProgress, DependencyProgressResponse DependencyProgress, bool DependenciesSatisfied, int PendingDependencyCount);

internal static class TasksEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/projects").WithTags("Tasks").RequireAuthorization(Policies.AuthenticatedUser);
        projects.MapPost("/{projectId:guid}/tasks", Create).RequireAuthorization(Policies.Admin);
        projects.MapGet("/{projectId:guid}/tasks", List);
        var tasks = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization(Policies.AuthenticatedUser);
        tasks.MapGet("/{id:guid}", Get);
        tasks.MapPut("/{id:guid}", Update).RequireAuthorization(Policies.Admin);
        tasks.MapPut("/{id:guid}/stage", Move).RequireAuthorization(Policies.Admin);
        tasks.MapPost("/{id:guid}/assignees", Add).RequireAuthorization(Policies.Admin);
        tasks.MapDelete("/{id:guid}/assignees/{userId:guid}", Remove).RequireAuthorization(Policies.Admin);
        tasks.MapPost("/{id:guid}/start", (Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct) => Flow(id, request, db, clock, activities, notifications, currentUser, ct, 1));
        tasks.MapPost("/{id:guid}/submit-review", (Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct) => Flow(id, request, db, clock, activities, notifications, currentUser, ct, 2));
        tasks.MapPost("/{id:guid}/request-changes", (Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct) => Flow(id, request, db, clock, activities, notifications, currentUser, ct, 3)).RequireAuthorization(Policies.Admin);
        tasks.MapPost("/{id:guid}/complete", (Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct) => Flow(id, request, db, clock, activities, notifications, currentUser, ct, 4)).RequireAuthorization(Policies.Admin);
        tasks.MapPost("/{id:guid}/cancel", (Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct) => Flow(id, request, db, clock, activities, notifications, currentUser, ct, 5)).RequireAuthorization(Policies.Admin);
    }

    private static IResult Problem(string type, string title, int status, string? detail = null) => Results.Problem(type: $"https://nakama/errors/{type}", title: title, detail: detail, statusCode: status);
    private static bool IsClosed(Project project) => project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;

    private static async Task<IResult> Create(Guid projectId, CreateTaskRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return Problem("project-not-found", "No existe el proyecto.", 404);
        if (IsClosed(project)) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.StageId is not { } stageId) return Problem("task-validation", "StageId es obligatorio.", 400);
        var creatorId = currentUser.UserId;
        var stage = await db.Stages.SingleOrDefaultAsync(x => x.Id == stageId, ct);
        if (stage is null) return Problem("stage-not-found", "No existe la etapa.", 404);
        if (stage.ProjectId != projectId) return Problem("task-stage-project-mismatch", "La etapa no pertenece al proyecto.", 409);
        if (!stage.IsActive) return Problem("task-stage-inactive", "La etapa está inactiva.", 409);
        var creator = await db.Users.SingleOrDefaultAsync(x => x.Id == creatorId, ct);
        if (creator is null) return Problem("user-not-found", "No existe el usuario.", 404);
        if (!creator.IsActive) return Problem("task-creator-inactive", "El creador está inactivo.", 409);
        if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == creatorId, ct)) return Problem("task-creator-not-project-member", "El creador no pertenece al proyecto.", 409);
        if (!Enum.TryParse(request.Priority, false, out TaskPriority priority) || !Enum.IsDefined(priority)) return Problem("task-validation", "Priority es obligatoria y válida.", 400);
        var assigneeIds = request.AssigneeIds ?? [];
        if (assigneeIds.Distinct().Count() != assigneeIds.Count) return Problem("task-validation", "AssigneeIds no puede contener duplicados.", 400);
        var assignees = await db.Users.Where(x => assigneeIds.Contains(x.Id)).ToListAsync(ct);
        if (assignees.Count != assigneeIds.Count) return Problem("task-assignee-not-found", "No existe un responsable.", 404);
        if (assignees.Any(x => !x.IsActive)) return Problem("task-assignee-inactive", "Un responsable está inactivo.", 409);
        if (await db.ProjectMembers.CountAsync(x => x.ProjectId == projectId && assigneeIds.Contains(x.UserId), ct) != assigneeIds.Count) return Problem("task-assignee-not-project-member", "Un responsable no pertenece al proyecto.", 409);
        try
        {
            var task = WorkTask.Create(projectId, stageId, request.Title ?? "", request.Description, priority, request.DueDate, creatorId, clock);
            db.Tasks.Add(task);
            db.TaskAssignees.AddRange(assigneeIds.Select(userId => TaskAssignee.Create(task.Id, userId, clock)));
            activities.Record(projectId, task.Id, ActivityType.TaskCreated, new { task.Title, stageId });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/tasks/{task.Id}", await Response(task, db, ct));
        }
        catch (ArgumentException exception) { return Problem("task-validation", "Los datos no son válidos.", 400, exception.Message); }
    }

    private static async Task<IResult> Get(Guid id, NakamaDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (!await ProjectAccess.CanAccessAsync(db, task.ProjectId, currentUser, ct)) return Problem("task-forbidden", "No tienes acceso a esta tarea.", 403);
        return Results.Ok(await Response(task, db, ct));
    }

    private static async Task<IResult> List(Guid projectId, Guid? stageId, string? status, string? priority, Guid? assigneeId, NakamaDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId, ct)) return Problem("project-not-found", "No existe el proyecto.", 404);
        if (!await ProjectAccess.CanAccessAsync(db, projectId, currentUser, ct)) return Problem("project-forbidden", "No tienes acceso a este proyecto.", 403);
        IQueryable<WorkTask> query = db.Tasks.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (stageId is { } stage) query = query.Where(x => x.StageId == stage);
        if (status is not null)
        {
            if (!Enum.TryParse(status, false, out DomainTaskStatus parsedStatus) || !Enum.IsDefined(parsedStatus)) return Problem("task-validation", "Status inválido.", 400);
            query = query.Where(x => x.Status == parsedStatus);
        }
        if (priority is not null)
        {
            if (!Enum.TryParse(priority, false, out TaskPriority parsedPriority) || !Enum.IsDefined(parsedPriority)) return Problem("task-validation", "Priority inválida.", 400);
            query = query.Where(x => x.Priority == parsedPriority);
        }
        if (assigneeId is { } assignee) query = query.Where(x => db.TaskAssignees.Any(member => member.TaskId == x.Id && member.UserId == assignee));
        var tasks = await query.OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Results.Ok(await Responses(tasks, db, ct));
    }

    private static async Task<IResult> Update(Guid id, UpdateTaskRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (IsClosed(await db.Projects.SingleAsync(x => x.Id == task.ProjectId, ct))) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        if (!Enum.TryParse(request.Priority, false, out TaskPriority priority) || !Enum.IsDefined(priority)) return Problem("task-validation", "Priority inválida.", 400);
        try { task.UpdateDetails(request.Title ?? "", request.Description, priority, request.DueDate, clock); activities.Record(task.ProjectId, task.Id, ActivityType.TaskUpdated); db.Entry(task).Property(x => x.Version).OriginalValue = request.Version.Value; await db.SaveChangesAsync(ct); return Results.NoContent(); }
        catch (ArgumentException exception) { return Problem("task-validation", "Datos inválidos.", 400, exception.Message); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static async Task<IResult> Move(Guid id, MoveTaskRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (IsClosed(await db.Projects.SingleAsync(x => x.Id == task.ProjectId, ct))) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        var stage = await db.Stages.SingleOrDefaultAsync(x => x.Id == request.StageId, ct);
        if (stage is null) return Problem("stage-not-found", "No existe la etapa.", 404);
        if (stage.ProjectId != task.ProjectId) return Problem("task-stage-project-mismatch", "La etapa no pertenece al proyecto.", 409);
        if (!stage.IsActive) return Problem("task-stage-inactive", "La etapa está inactiva.", 409);
        task.MoveToStage(stage.Id, clock); activities.Record(task.ProjectId, task.Id, ActivityType.TaskMoved, new { stageId = stage.Id }); db.Entry(task).Property(x => x.Version).OriginalValue = request.Version.Value;
        try { await db.SaveChangesAsync(ct); return Results.NoContent(); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static async Task<IResult> Add(Guid id, AddAssigneeRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (IsClosed(await db.Projects.SingleAsync(x => x.Id == task.ProjectId, ct))) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        if (request.UserId is not { } userId) return Problem("task-validation", "UserId es obligatorio.", 400);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Problem("user-not-found", "No existe el usuario.", 404);
        if (!user.IsActive) return Problem("task-assignee-inactive", "El usuario está inactivo.", 409);
        if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == userId, ct)) return Problem("task-assignee-not-project-member", "El usuario no pertenece al proyecto.", 409);
        if (await db.TaskAssignees.AnyAsync(x => x.TaskId == id && x.UserId == userId, ct)) return Problem("task-assignee-already-exists", "El usuario ya está asignado.", 409);
        db.TaskAssignees.Add(TaskAssignee.Create(id, userId, clock)); task.ChangeAssignments(clock); activities.Record(task.ProjectId, task.Id, ActivityType.TaskAssigneeAdded, new { assigneeUserId = userId }); await notifications.WriteAsync([userId], currentUser.UserId, NotificationType.TaskAssigned, task.ProjectId, task.Id, new { taskTitle = task.Title }, ct); db.Entry(task).Property(x => x.Version).OriginalValue = request.Version.Value;
        try { await db.SaveChangesAsync(ct); return Results.Created($"/api/tasks/{id}/assignees/{userId}", new AssigneeResponse(userId, user.FullName, user.Email)); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static async Task<IResult> Remove(Guid id, Guid userId, [FromBody] VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (IsClosed(await db.Projects.SingleAsync(x => x.Id == task.ProjectId, ct))) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        var assignee = await db.TaskAssignees.SingleOrDefaultAsync(x => x.TaskId == id && x.UserId == userId, ct);
        if (assignee is null) return Problem("task-assignee-not-found", "El usuario no está asignado.", 404);
        db.TaskAssignees.Remove(assignee); task.ChangeAssignments(clock); activities.Record(task.ProjectId, task.Id, ActivityType.TaskAssigneeRemoved, new { assigneeUserId = userId }); await notifications.WriteAsync([userId], currentUser.UserId, NotificationType.TaskUnassigned, task.ProjectId, task.Id, new { taskTitle = task.Title }, ct); db.Entry(task).Property(x => x.Version).OriginalValue = request.Version.Value;
        try { await db.SaveChangesAsync(ct); return Results.NoContent(); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static async Task<IResult> Flow(Guid id, VersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, INotificationWriter notifications, ICurrentUser currentUser, CancellationToken ct, int operation)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        if (!await ProjectAccess.CanAccessAsync(db, task.ProjectId, currentUser, ct)) return Problem("task-forbidden", "No tienes acceso a esta tarea.", 403);
        if (IsClosed(await db.Projects.SingleAsync(x => x.Id == task.ProjectId, ct))) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        var hasUnsatisfiedDependencies = await HasUnsatisfiedDependenciesAsync(id, db, ct);
        if ((operation is 1 or 2 or 4) && hasUnsatisfiedDependencies) return Problem("task-dependencies-not-satisfied", "Las dependencias no están satisfechas.", 409);
        try
        {
            if (operation == 1) task.Start(clock); if (operation == 2) task.SubmitForReview(clock); if (operation == 3) task.RequestChanges(clock); if (operation == 4) task.Complete(clock); if (operation == 5) task.Cancel(clock);
            var activityType = operation switch { 1 => ActivityType.TaskStarted, 2 => ActivityType.TaskSubmittedForReview, 3 => ActivityType.TaskChangesRequested, 4 => ActivityType.TaskCompleted, 5 => ActivityType.TaskCancelled, _ => throw new InvalidOperationException() };
            activities.Record(task.ProjectId, task.Id, activityType);
            if (operation is 2 or 3 or 4)
            {
                var recipients = operation == 2
                    ? await db.ProjectMembers.Where(x => x.ProjectId == task.ProjectId && x.Role == ProjectRole.Owner).Select(x => x.UserId).ToListAsync(ct)
                    : await db.TaskAssignees.Where(x => x.TaskId == task.Id).Select(x => x.UserId).ToListAsync(ct);
                var type = operation == 2 ? NotificationType.TaskSubmittedForReview : operation == 3 ? NotificationType.TaskChangesRequested : NotificationType.TaskCompleted;
                await notifications.WriteAsync(recipients, currentUser.UserId, type, task.ProjectId, task.Id, new { taskTitle = task.Title }, ct);
            }
            db.Entry(task).Property(x => x.Version).OriginalValue = request.Version.Value; await db.SaveChangesAsync(ct); return Results.NoContent();
        }
        catch (InvalidOperationException exception) { return Problem("task-invalid-status-transition", "La transición no es válida.", 409, exception.Message); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static Task<bool> HasUnsatisfiedDependenciesAsync(Guid taskId, NakamaDbContext db, CancellationToken ct) => db.TaskDependencies.Join(db.Tasks, dependency => dependency.DependsOnTaskId, prerequisite => prerequisite.Id, (dependency, prerequisite) => new { dependency, prerequisite }).AnyAsync(x => x.dependency.TaskId == taskId && x.prerequisite.Status != DomainTaskStatus.Completed, ct);

    private static async Task<IReadOnlyList<TaskResponse>> Responses(IReadOnlyList<WorkTask> tasks, NakamaDbContext db, CancellationToken ct)
    {
        if (tasks.Count == 0) return [];
        var taskIds = tasks.Select(x => x.Id).ToArray();
        var assignees = await (from member in db.TaskAssignees.AsNoTracking() join user in db.Users.AsNoTracking() on member.UserId equals user.Id where taskIds.Contains(member.TaskId) select new { member.TaskId, Assignee = new AssigneeResponse(user.Id, user.FullName, user.Email) }).ToListAsync(ct);
        var subtaskProgress = await db.Subtasks.AsNoTracking().Where(x => taskIds.Contains(x.TaskId)).GroupBy(x => x.TaskId).Select(group => new { TaskId = group.Key, Completed = group.Count(x => x.IsCompleted), Total = group.Count() }).ToDictionaryAsync(x => x.TaskId, ct);
        var dependencyProgress = await (from dependency in db.TaskDependencies.AsNoTracking()
                                        join prerequisite in db.Tasks.AsNoTracking() on dependency.DependsOnTaskId equals prerequisite.Id
                                        where taskIds.Contains(dependency.TaskId)
                                        group prerequisite by dependency.TaskId into dependencyGroup
                                        select new
                                        {
                                            TaskId = dependencyGroup.Key,
                                            Satisfied = dependencyGroup.Count(x => x.Status == DomainTaskStatus.Completed),
                                            Total = dependencyGroup.Count()
                                        }).ToDictionaryAsync(x => x.TaskId, ct);
        return tasks.Select(task =>
        {
            var subtasks = subtaskProgress.TryGetValue(task.Id, out var subtask) ? new SubtaskProgressResponse(subtask.Completed, subtask.Total) : new SubtaskProgressResponse(0, 0);
            var dependencies = dependencyProgress.TryGetValue(task.Id, out var dependency) ? new DependencyProgressResponse(dependency.Satisfied, dependency.Total) : new DependencyProgressResponse(0, 0);
            var pendingDependencyCount = dependencies.Total - dependencies.Satisfied;
            return new TaskResponse(task.Id, task.ProjectId, task.StageId, task.Title, task.Description, task.Status.ToString(), task.Priority.ToString(), task.DueDate, assignees.Where(x => x.TaskId == task.Id).Select(x => x.Assignee).ToList(), task.Version, task.CreatedAt, task.UpdatedAt, task.CompletedAt, subtasks, dependencies, pendingDependencyCount == 0, pendingDependencyCount);
        }).ToList();
    }

    private static async Task<TaskResponse> Response(WorkTask task, NakamaDbContext db, CancellationToken ct) => (await Responses([task], db, ct))[0];
}
