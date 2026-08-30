using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record ReportTaskBlockerRequest(string? Type, string? Description, Guid? ReportedByUserId, long? Version);
public sealed record ResolveTaskBlockerRequest(Guid? ResolvedByUserId, long? Version);
public sealed record BlockerUserResponse(Guid Id, string FullName);
public sealed record TaskBlockerResponse(Guid Id, Guid TaskId, string Type, string Description, BlockerUserResponse ReportedBy, DateTimeOffset ReportedAt, BlockerUserResponse? ResolvedBy, DateTimeOffset? ResolvedAt, bool IsResolved, long? TaskVersion);

internal static class TaskBlockersEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Task Blockers");
        group.MapPost("/{taskId:guid}/blockers", Report);
        group.MapGet("/{taskId:guid}/blockers", List);
        group.MapPost("/{taskId:guid}/blockers/{blockerId:guid}/resolve", Resolve);
    }

    private static IResult Problem(string type, string title, int status, string? detail = null) => Results.Problem(type: $"https://nakama/errors/{type}", title: title, detail: detail, statusCode: status);
    private static bool IsClosed(Project project) => project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;

    private static async Task<IResult> Report(Guid taskId, ReportTaskBlockerRequest request, NakamaDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        var project = await db.Projects.SingleAsync(project => project.Id == task.ProjectId, cancellationToken);
        if (IsClosed(project)) return Problem("task-project-closed", "El proyecto está cerrado.", 409);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        if (request.ReportedByUserId is not { } reporterId) return Problem("task-validation", "ReportedByUserId es obligatorio.", 400);
        if (!Enum.TryParse(request.Type, false, out TaskBlockerType type) || !Enum.IsDefined(type)) return Problem("task-validation", "Type no es válido.", 400);
        var reporter = await db.Users.SingleOrDefaultAsync(user => user.Id == reporterId, cancellationToken);
        if (reporter is null) return Problem("user-not-found", "No existe el usuario.", 404);
        if (!reporter.IsActive) return Problem("task-blocker-user-inactive", "El usuario está inactivo.", 409);
        if (!await db.ProjectMembers.AnyAsync(member => member.ProjectId == task.ProjectId && member.UserId == reporterId, cancellationToken)) return Problem("task-blocker-user-not-project-member", "El usuario no pertenece al proyecto.", 409);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var blocker = TaskBlocker.Report(task.Id, type, request.Description ?? string.Empty, reporterId, clock);
            task.Block(clock);
            db.Entry(task).Property(current => current.Version).OriginalValue = request.Version.Value;
            db.TaskBlockers.Add(blocker);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/tasks/{task.Id}/blockers/{blocker.Id}", Response(blocker, reporter, null, task.Version));
        }
        catch (InvalidOperationException exception) { return Problem("task-cannot-be-blocked", "La tarea no puede bloquearse.", 409, exception.Message); }
        catch (ArgumentException exception) { return Problem("task-validation", "Los datos no son válidos.", 400, exception.Message); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static async Task<IResult> List(Guid taskId, bool? active, NakamaDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Tasks.AsNoTracking().AnyAsync(task => task.Id == taskId, cancellationToken)) return Problem("task-not-found", "No existe la tarea.", 404);
        var query = db.TaskBlockers.AsNoTracking().Where(blocker => blocker.TaskId == taskId);
        if (active is true) query = query.Where(blocker => blocker.ResolvedAt == null);
        if (active is false) query = query.Where(blocker => blocker.ResolvedAt != null);
        var blockers = await query.OrderByDescending(blocker => blocker.ReportedAt).ToListAsync(cancellationToken);
        var userIds = blockers.Select(blocker => blocker.ReportedByUserId).Concat(blockers.Where(blocker => blocker.ResolvedByUserId.HasValue).Select(blocker => blocker.ResolvedByUserId!.Value)).Distinct();
        var users = await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, cancellationToken);
        return Results.Ok(blockers.Select(blocker => Response(blocker, users[blocker.ReportedByUserId], blocker.ResolvedByUserId is { } id ? users[id] : null, null)));
    }

    private static async Task<IResult> Resolve(Guid taskId, Guid blockerId, ResolveTaskBlockerRequest request, NakamaDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);
        if (task is null) return Problem("task-not-found", "No existe la tarea.", 404);
        var blocker = await db.TaskBlockers.SingleOrDefaultAsync(blocker => blocker.Id == blockerId && blocker.TaskId == taskId, cancellationToken);
        if (blocker is null) return Problem("task-blocker-not-found", "No existe el bloqueo.", 404);
        if (request.Version is not > 0 || request.Version != task.Version) return Problem("task-version-conflict", "La tarea fue modificada.", 409);
        if (blocker.IsResolved) return Problem("task-blocker-already-resolved", "El bloqueo ya fue resuelto.", 409);
        if (task.Status is not (DomainTaskStatus.Blocked or DomainTaskStatus.Cancelled)) return Problem("task-not-blocked", "La tarea no está bloqueada.", 409);
        if (request.ResolvedByUserId is not { } resolverId) return Problem("task-validation", "ResolvedByUserId es obligatorio.", 400);
        var resolver = await db.Users.SingleOrDefaultAsync(user => user.Id == resolverId, cancellationToken);
        if (resolver is null) return Problem("user-not-found", "No existe el usuario.", 404);
        if (!resolver.IsActive) return Problem("task-blocker-user-inactive", "El usuario está inactivo.", 409);
        if (!await db.ProjectMembers.AnyAsync(member => member.ProjectId == task.ProjectId && member.UserId == resolverId, cancellationToken)) return Problem("task-blocker-user-not-project-member", "El usuario no pertenece al proyecto.", 409);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            blocker.Resolve(resolverId, clock);
            var otherActive = await db.TaskBlockers.AnyAsync(current => current.TaskId == taskId && current.Id != blockerId && current.ResolvedAt == null, cancellationToken);
            task.ResolveBlocker(otherActive, clock);
            db.Entry(task).Property(current => current.Version).OriginalValue = request.Version.Value;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var reporter = await db.Users.AsNoTracking().SingleAsync(user => user.Id == blocker.ReportedByUserId, cancellationToken);
            return Results.Ok(Response(blocker, reporter, resolver, task.Version));
        }
        catch (InvalidOperationException exception) { return Problem("task-blocker-already-resolved", "El bloqueo ya fue resuelto.", 409, exception.Message); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", "La tarea fue modificada.", 409); }
    }

    private static TaskBlockerResponse Response(TaskBlocker blocker, User reporter, User? resolver, long? taskVersion) => new(blocker.Id, blocker.TaskId, blocker.Type.ToString(), blocker.Description, new BlockerUserResponse(reporter.Id, reporter.FullName), blocker.ReportedAt, resolver is null ? null : new BlockerUserResponse(resolver.Id, resolver.FullName), blocker.ResolvedAt, blocker.IsResolved, taskVersion);
}
