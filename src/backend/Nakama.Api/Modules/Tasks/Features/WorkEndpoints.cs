using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Tasks.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record WorkProjectResponse(Guid Id, string Name);
public sealed record WorkStageResponse(Guid Id, string Name);
public sealed record WorkSubtaskProgressResponse(int Completed, int Total);
public sealed record WorkItemResponse(Guid Id, string Title, string Status, string Priority, DateTimeOffset? DueDate, WorkProjectResponse Project, WorkStageResponse Stage, WorkSubtaskProgressResponse SubtaskProgress, int PendingDependencyCount, int ActiveBlockerCount, long Version);
public sealed record WorkFeedResponse(IReadOnlyList<WorkItemResponse> Items, string? NextCursor);

internal static class WorkEndpoints
{
    private const int DefaultPageSize = 30;
    public static void MapEndpoints(IEndpointRouteBuilder app) => app.MapGet("/api/me/work", Get).WithTags("My Work").RequireAuthorization(Policies.AuthenticatedUser);

    private static async Task<IResult> Get(string? status, Guid? projectId, string? priority, string? cursor, int? limit, ICurrentUser currentUser, NakamaDbContext db, CancellationToken ct)
    {
        if (!TryParseStatus(status, out var parsedStatus) || !TryParsePriority(priority, out var parsedPriority))
            return Results.Problem(type: "https://nakama/errors/work-filter-invalid", title: "El filtro no es válido.", statusCode: StatusCodes.Status400BadRequest);
        var position = DecodeCursor(cursor);
        if (cursor is not null && position is null)
            return Results.Problem(type: "https://nakama/errors/work-cursor-invalid", title: "El cursor no es válido.", statusCode: StatusCodes.Status400BadRequest);

        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, 100);
        var query = from task in db.Tasks.AsNoTracking()
                    join assignee in db.TaskAssignees.AsNoTracking() on task.Id equals assignee.TaskId
                    join project in db.Projects.AsNoTracking() on task.ProjectId equals project.Id
                    join stage in db.Stages.AsNoTracking() on task.StageId equals stage.Id
                    where assignee.UserId == currentUser.UserId
                    select new { Task = task, Project = project, Stage = stage, PriorityRank = task.Priority == TaskPriority.Critical ? 0 : task.Priority == TaskPriority.High ? 1 : task.Priority == TaskPriority.Medium ? 2 : 3 };
        if (parsedStatus is { } workStatus) query = query.Where(item => item.Task.Status == workStatus);
        else query = query.Where(item => item.Task.Status != DomainTaskStatus.Completed && item.Task.Status != DomainTaskStatus.Cancelled);
        if (parsedPriority is { } workPriority) query = query.Where(item => item.Task.Priority == workPriority);
        if (projectId is { } requestedProject) query = query.Where(item => item.Task.ProjectId == requestedProject);
        if (position is { } after)
        {
            query = after.DueDate is null
                ? query.Where(item => item.PriorityRank > after.PriorityRank || item.PriorityRank == after.PriorityRank && item.Task.DueDate == null && item.Task.Id.CompareTo(after.Id) > 0)
                : query.Where(item => item.PriorityRank > after.PriorityRank || item.PriorityRank == after.PriorityRank && (item.Task.DueDate == null || item.Task.DueDate > after.DueDate || item.Task.DueDate == after.DueDate && item.Task.Id.CompareTo(after.Id) > 0));
        }

        var page = await query.OrderBy(item => item.PriorityRank).ThenBy(item => item.Task.DueDate == null).ThenBy(item => item.Task.DueDate).ThenBy(item => item.Task.Id).Take(pageSize + 1).ToListAsync(ct);
        var hasNext = page.Count > pageSize;
        var rows = page.Take(pageSize).ToList();
        var ids = rows.Select(item => item.Task.Id).ToArray();
        if (ids.Length == 0) return Results.Ok(new WorkFeedResponse([], null));
        var subtasks = await db.Subtasks.AsNoTracking().Where(item => ids.Contains(item.TaskId)).GroupBy(item => item.TaskId).Select(group => new { TaskId = group.Key, Completed = group.Count(item => item.IsCompleted), Total = group.Count() }).ToDictionaryAsync(item => item.TaskId, ct);
        var dependencies = await (from dependency in db.TaskDependencies.AsNoTracking()
                                  join prerequisite in db.Tasks.AsNoTracking() on dependency.DependsOnTaskId equals prerequisite.Id
                                  where ids.Contains(dependency.TaskId) && prerequisite.Status != DomainTaskStatus.Completed
                                  group dependency by dependency.TaskId into dependencyGroup
                                  select new { TaskId = dependencyGroup.Key, Count = dependencyGroup.Count() }).ToDictionaryAsync(item => item.TaskId, item => item.Count, ct);
        var blockers = await db.TaskBlockers.AsNoTracking().Where(item => ids.Contains(item.TaskId) && item.ResolvedAt == null).GroupBy(item => item.TaskId).Select(group => new { TaskId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.TaskId, item => item.Count, ct);
        var items = rows.Select(item => new WorkItemResponse(item.Task.Id, item.Task.Title, item.Task.Status.ToString(), item.Task.Priority.ToString(), item.Task.DueDate, new(item.Project.Id, item.Project.Name), new(item.Stage.Id, item.Stage.Name), subtasks.TryGetValue(item.Task.Id, out var progress) ? new(progress.Completed, progress.Total) : new(0, 0), dependencies.GetValueOrDefault(item.Task.Id), blockers.GetValueOrDefault(item.Task.Id), item.Task.Version)).ToList();
        var last = rows.LastOrDefault();
        return Results.Ok(new WorkFeedResponse(items, hasNext && last is not null ? EncodeCursor(last.PriorityRank, last.Task.DueDate, last.Task.Id) : null));
    }

    private static bool TryParseStatus(string? value, out DomainTaskStatus? result) { result = null; if (value is null) return true; if (!Enum.TryParse<DomainTaskStatus>(value, false, out var parsed) || !Enum.IsDefined(parsed)) return false; result = parsed; return true; }
    private static bool TryParsePriority(string? value, out TaskPriority? result) { result = null; if (value is null) return true; if (!Enum.TryParse<TaskPriority>(value, false, out var parsed) || !Enum.IsDefined(parsed)) return false; result = parsed; return true; }
    private static string EncodeCursor(int priorityRank, DateTimeOffset? dueDate, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new WorkCursor(priorityRank, dueDate, id))));
    private static WorkCursor? DecodeCursor(string? cursor) { if (string.IsNullOrWhiteSpace(cursor)) return null; try { return JsonSerializer.Deserialize<WorkCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); } catch (FormatException) { return null; } catch (JsonException) { return null; } }
    private sealed record WorkCursor(int PriorityRank, DateTimeOffset? DueDate, Guid Id);
}
