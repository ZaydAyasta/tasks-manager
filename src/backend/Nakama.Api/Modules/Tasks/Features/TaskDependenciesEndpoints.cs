using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Tasks.Domain;
using TaskState = Nakama.Api.Modules.Tasks.Domain.TaskStatus;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record CreateDependencyRequest(Guid? DependsOnTaskId, Guid? CreatedByUserId, long? TaskVersion);
public sealed record DependencyVersionRequest(long? TaskVersion);
public sealed record DependencyTaskResponse(Guid Id, string Title, string Status);
public sealed record DependencyUserResponse(Guid Id, string FullName, string Email);
public sealed record DependencyResponse(Guid Id, DependencyTaskResponse DependsOnTask, bool IsSatisfied, DependencyUserResponse CreatedBy, DateTimeOffset CreatedAt);

internal static class TaskDependenciesEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Task Dependencies");
        group.MapPost("/{id:guid}/dependencies", Create);
        group.MapGet("/{id:guid}/dependencies", List);
        group.MapGet("/{id:guid}/dependents", Dependents);
        group.MapDelete("/{id:guid}/dependencies/{dependencyId:guid}", Delete);
    }

    private static IResult Problem(string type, int status) => Results.Problem(type: $"https://nakama/errors/{type}", statusCode: status);
    private static bool IsEditable(WorkTask task) => task.Status is TaskState.Pending or TaskState.InProgress or TaskState.Blocked;

    private static async Task<IResult> Create(Guid id, CreateDependencyRequest request, NakamaDbContext db, IClock clock, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", 404);
        if (!IsEditable(task)) return Problem("task-dependencies-read-only", 409);
        if (request.TaskVersion is not > 0 || request.TaskVersion != task.Version) return Problem("task-version-conflict", 409);
        if (request.DependsOnTaskId is not { } prerequisiteId || prerequisiteId == id) return Problem("task-dependency-self-reference", 409);
        var prerequisite = await db.Tasks.SingleOrDefaultAsync(x => x.Id == prerequisiteId, ct);
        if (prerequisite is null) return Problem("task-not-found", 404);
        if (prerequisite.ProjectId != task.ProjectId) return Problem("task-dependency-project-mismatch", 409);
        if (request.CreatedByUserId is not { } userId || !await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) return Problem("task-dependency-user-inactive", 409);
        if (!await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == userId, ct)) return Problem("task-dependency-user-not-project-member", 409);
        if (await db.TaskDependencies.AnyAsync(x => x.TaskId == id && x.DependsOnTaskId == prerequisiteId, ct)) return Problem("task-dependency-already-exists", 409);

        var projectTaskIds = await db.Tasks.Where(x => x.ProjectId == task.ProjectId).Select(x => x.Id).ToListAsync(ct);
        var edges = await db.TaskDependencies.Where(x => projectTaskIds.Contains(x.TaskId)).Select(x => new { x.TaskId, x.DependsOnTaskId }).ToListAsync(ct);
        if (CreatesCycle(id, prerequisiteId, edges.Select(x => (x.TaskId, x.DependsOnTaskId)))) return Problem("task-dependency-cycle", 409);

        db.TaskDependencies.Add(TaskDependency.Create(id, prerequisiteId, userId, clock));
        task.ChangeAssignments(clock);
        db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion.Value;
        try
        {
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/tasks/{id}/dependencies", null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem("task-version-conflict", 409);
        }
    }

    internal static bool CreatesCycle(Guid taskId, Guid prerequisiteId, IEnumerable<(Guid TaskId, Guid DependsOnTaskId)> edges)
    {
        var adjacency = edges.GroupBy(edge => edge.TaskId).ToDictionary(group => group.Key, group => group.Select(edge => edge.DependsOnTaskId));
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(prerequisiteId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == taskId) return true;
            if (visited.Add(current) && adjacency.TryGetValue(current, out var next))
                foreach (var nextId in next) pending.Push(nextId);
        }
        return false;
    }

    private static async Task<IResult> List(Guid id, NakamaDbContext db, CancellationToken ct)
    {
        if (!await db.Tasks.AsNoTracking().AnyAsync(task => task.Id == id, ct)) return Problem("task-not-found", 404);
        var dependencies = await (from dependency in db.TaskDependencies.AsNoTracking()
                                  join prerequisite in db.Tasks.AsNoTracking() on dependency.DependsOnTaskId equals prerequisite.Id
                                  join creator in db.Users.AsNoTracking() on dependency.CreatedByUserId equals creator.Id
                                  where dependency.TaskId == id
                                  select new DependencyResponse(
                                      dependency.Id,
                                      new DependencyTaskResponse(prerequisite.Id, prerequisite.Title, prerequisite.Status.ToString()),
                                      prerequisite.Status == TaskState.Completed,
                                      new DependencyUserResponse(creator.Id, creator.FullName, creator.Email),
                                      dependency.CreatedAt)).ToListAsync(ct);
        return Results.Ok(dependencies);
    }
    private static async Task<IResult> Dependents(Guid id, NakamaDbContext db, CancellationToken ct)
    {
        var dependents = await (from dependency in db.TaskDependencies
                                join task in db.Tasks on dependency.TaskId equals task.Id
                                where dependency.DependsOnTaskId == id
                                select new { dependencyId = dependency.Id, task = new DependencyTaskResponse(task.Id, task.Title, task.Status.ToString()) })
            .ToListAsync(ct);
        return Results.Ok(dependents);
    }
    private static async Task<IResult> Delete(Guid id, Guid dependencyId, [FromBody] DependencyVersionRequest request, NakamaDbContext db, IClock clock, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task is null) return Problem("task-not-found", 404);
        if (!IsEditable(task)) return Problem("task-dependencies-read-only", 409);
        if (request.TaskVersion is not > 0 || request.TaskVersion != task.Version) return Problem("task-version-conflict", 409);
        var dependency = await db.TaskDependencies.SingleOrDefaultAsync(x => x.Id == dependencyId && x.TaskId == id, ct);
        if (dependency is null) return Problem("task-dependency-not-found", 404);
        db.TaskDependencies.Remove(dependency);
        task.ChangeAssignments(clock);
        db.Entry(task).Property(x => x.Version).OriginalValue = request.TaskVersion.Value;
        try
        {
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem("task-version-conflict", 409);
        }
    }
}
