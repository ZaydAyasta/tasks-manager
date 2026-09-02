using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Tasks.Domain;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record CreateSubtaskRequest(string? Title, long? TaskVersion);
public sealed record UpdateSubtaskRequest(string? Title, long? TaskVersion);
public sealed record SubtaskActionRequest(long? TaskVersion);
public sealed record SubtaskVersionRequest(long? TaskVersion);
public sealed record SubtaskOrderItem(Guid SubtaskId, int Position);
public sealed record ReorderSubtasksRequest(long? TaskVersion, IReadOnlyList<SubtaskOrderItem>? Subtasks);
public sealed record SubtaskUserResponse(Guid Id, string FullName);
public sealed record SubtaskResponse(Guid Id, Guid TaskId, string Title, int Position, bool IsCompleted, SubtaskUserResponse CreatedBy, DateTimeOffset CreatedAt, SubtaskUserResponse? CompletedBy, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt, long? TaskVersion);

internal static class SubtasksEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Subtasks").RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPost("/{taskId:guid}/subtasks", Create);
        group.MapGet("/{taskId:guid}/subtasks", List);
        group.MapPut("/{taskId:guid}/subtasks/{id:guid}", Update);
        group.MapDelete("/{taskId:guid}/subtasks/{id:guid}", Delete);
        group.MapPost("/{taskId:guid}/subtasks/{id:guid}/complete", Complete);
        group.MapPost("/{taskId:guid}/subtasks/{id:guid}/reopen", Reopen);
        group.MapPut("/{taskId:guid}/subtasks/order", Order);
    }

    private static IResult Problem(string type, int status) => Results.Problem(type: $"https://nakama/errors/{type}", statusCode: status);
    private static bool IsMutable(WorkTask task) => task.Status is DomainTaskStatus.Pending or DomainTaskStatus.InProgress or DomainTaskStatus.Blocked;

    private static async Task<(WorkTask? Task, IResult? Error)> GetMutableTask(Guid taskId, long? version, NakamaDbContext db, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null) return (null, Problem("task-not-found", 404));
        if (!IsMutable(task)) return (null, Problem("task-subtasks-read-only", 409));
        if (version is not > 0 || version != task.Version) return (null, Problem("task-version-conflict", 409));
        return (task, null);
    }

    private static async Task<IResult> Create(Guid taskId, CreateSubtaskRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken ct)
    {
        var result = await GetMutableTask(taskId, request.TaskVersion, db, ct);
        if (result.Error is not null) return result.Error;
        var task = result.Task!;
        var userId = currentUser.UserId;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Problem("user-not-found", 404);
        if (!user.IsActive) return Problem("subtask-user-inactive", 409);
        if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == userId, ct)) return Problem("subtask-user-not-project-member", 409);

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var position = (await db.Subtasks.Where(x => x.TaskId == taskId).MaxAsync(x => (int?)x.Position, ct) ?? 0) + 1;
            var subtask = Subtask.Create(taskId, request.Title ?? "", position, userId, clock);
            db.Subtasks.Add(subtask);
            activities.Record(task.ProjectId, task.Id, ActivityType.SubtaskCreated, new { subtaskId = subtask.Id, subtask.Title });
            task.ChangeAssignments(clock);
            db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion!.Value;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/tasks/{taskId}/subtasks/{subtask.Id}", new SubtaskResponse(subtask.Id, taskId, subtask.Title, subtask.Position, false, new(userId, user.FullName), subtask.CreatedAt, null, null, subtask.UpdatedAt, task.Version));
        }
        catch (ArgumentException) { return Problem("task-validation", 400); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", 409); }
    }

    private static async Task<IResult> List(Guid taskId, NakamaDbContext db, CancellationToken ct)
    {
        if (!await db.Tasks.AnyAsync(x => x.Id == taskId, ct)) return Problem("task-not-found", 404);
        var subtasks = await db.Subtasks.AsNoTracking().Where(x => x.TaskId == taskId).OrderBy(x => x.Position).ToListAsync(ct);
        var userIds = subtasks.Select(x => x.CreatedByUserId).Concat(subtasks.Where(x => x.CompletedByUserId is not null).Select(x => x.CompletedByUserId!.Value)).Distinct();
        var users = await db.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return Results.Ok(subtasks.Select(subtask => new SubtaskResponse(subtask.Id, subtask.TaskId, subtask.Title, subtask.Position, subtask.IsCompleted, new(subtask.CreatedByUserId, users[subtask.CreatedByUserId].FullName), subtask.CreatedAt, subtask.CompletedByUserId is { } id ? new(id, users[id].FullName) : null, subtask.CompletedAt, subtask.UpdatedAt, null)));
    }

    private static async Task<IResult> Update(Guid taskId, Guid id, UpdateSubtaskRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, CancellationToken ct)
    {
        var result = await GetMutableTask(taskId, request.TaskVersion, db, ct);
        if (result.Error is not null) return result.Error;
        var task = result.Task!;
        var subtask = await db.Subtasks.SingleOrDefaultAsync(x => x.Id == id && x.TaskId == taskId, ct);
        if (subtask is null) return Problem("subtask-not-found", 404);
        try
        {
            subtask.UpdateTitle(request.Title ?? "", clock);
            activities.Record(task.ProjectId, task.Id, ActivityType.SubtaskUpdated, new { subtaskId = subtask.Id, subtask.Title });
            task.ChangeAssignments(clock);
            db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion!.Value;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (ArgumentException) { return Problem("task-validation", 400); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", 409); }
    }

    private static Task<IResult> Complete(Guid taskId, Guid id, SubtaskActionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken ct) => Act(taskId, id, request, db, clock, activities, currentUser, ct, true);
    private static Task<IResult> Reopen(Guid taskId, Guid id, SubtaskActionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken ct) => Act(taskId, id, request, db, clock, activities, currentUser, ct, false);

    private static async Task<IResult> Act(Guid taskId, Guid id, SubtaskActionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, ICurrentUser currentUser, CancellationToken ct, bool complete)
    {
        var result = await GetMutableTask(taskId, request.TaskVersion, db, ct);
        if (result.Error is not null) return result.Error;
        var task = result.Task!;
        var userId = currentUser.UserId;
        if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == userId, ct)) return Problem("subtask-user-not-project-member", 409);
        var subtask = await db.Subtasks.SingleOrDefaultAsync(x => x.Id == id && x.TaskId == taskId, ct);
        if (subtask is null) return Problem("subtask-not-found", 404);
        try
        {
            if (complete) subtask.Complete(userId, clock); else subtask.Reopen(clock);
            activities.Record(task.ProjectId, task.Id, complete ? ActivityType.SubtaskCompleted : ActivityType.SubtaskReopened, new { subtaskId = subtask.Id });
            task.ChangeAssignments(clock);
            db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion!.Value;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException) { return Problem(complete ? "subtask-already-completed" : "subtask-not-completed", 409); }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", 409); }
    }

    private static async Task<IResult> Delete(Guid taskId, Guid id, [FromBody] SubtaskVersionRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, CancellationToken ct)
    {
        var result = await GetMutableTask(taskId, request.TaskVersion, db, ct);
        if (result.Error is not null) return result.Error;
        var task = result.Task!;
        var subtask = await db.Subtasks.SingleOrDefaultAsync(x => x.Id == id && x.TaskId == taskId, ct);
        if (subtask is null) return Problem("subtask-not-found", 404);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Subtasks.Remove(subtask);
        activities.Record(task.ProjectId, task.Id, ActivityType.SubtaskDeleted, new { subtaskId = subtask.Id, subtask.Title });
        await db.SaveChangesAsync(ct);
        var remaining = await db.Subtasks.Where(x => x.TaskId == taskId).OrderBy(x => x.Position).ToListAsync(ct);
        for (var index = 0; index < remaining.Count; index++) remaining[index].Reposition(index + 1);
        task.ChangeAssignments(clock);
        db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion!.Value;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Order(Guid taskId, ReorderSubtasksRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activities, CancellationToken ct)
    {
        var result = await GetMutableTask(taskId, request.TaskVersion, db, ct);
        if (result.Error is not null) return result.Error;
        var task = result.Task!;
        var existing = await db.Subtasks.Where(x => x.TaskId == taskId).ToListAsync(ct);
        var order = request.Subtasks;
        if (order is null || order.Count != existing.Count || order.Select(x => x.SubtaskId).Distinct().Count() != order.Count || !order.Select(x => x.Position).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, order.Count)) || order.Any(x => existing.All(subtask => subtask.Id != x.SubtaskId))) return Problem("subtask-order-invalid", 400);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Subtasks.Where(x => x.TaskId == taskId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Position, x => x.Position + existing.Count), ct);
        foreach (var item in order)
            await db.Subtasks.Where(x => x.Id == item.SubtaskId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Position, item.Position).SetProperty(x => x.UpdatedAt, clock.UtcNow), ct);
        activities.Record(task.ProjectId, task.Id, ActivityType.SubtasksReordered);
        task.ChangeAssignments(clock);
        db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion!.Value;
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException) { return Problem("task-version-conflict", 409); }
    }
}
