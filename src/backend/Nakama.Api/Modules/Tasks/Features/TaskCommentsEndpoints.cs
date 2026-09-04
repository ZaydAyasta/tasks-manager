using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Tasks.Domain;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Notifications.Domain;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record CommentAuthorResponse(Guid Id, string FullName);
public sealed record TaskCommentResponse(Guid Id, string Content, CommentAuthorResponse Author, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, bool IsEdited);
public sealed record TaskCommentFeedResponse(IReadOnlyList<TaskCommentResponse> Items, string? NextCursor);
public sealed record WriteTaskCommentRequest(string? Content);

internal static class TaskCommentsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Task Comments").RequireAuthorization(Policies.AuthenticatedUser);
        group.MapGet("/{taskId:guid}/comments", List);
        group.MapPost("/{taskId:guid}/comments", Create);
        group.MapPut("/{taskId:guid}/comments/{commentId:guid}", Edit);
        group.MapDelete("/{taskId:guid}/comments/{commentId:guid}", Delete);
    }
    private static IResult Problem(string type, string title, int status) => Results.Problem(type: $"https://nakama/errors/{type}", title: title, statusCode: status);
    private static async Task<WorkTask?> AccessibleTask(Guid taskId, NakamaDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == taskId, ct);
        return task is not null && await db.ProjectMembers.AnyAsync(x => x.ProjectId == task.ProjectId && x.UserId == current.UserId, ct) ? task : null;
    }
    private static async Task<IResult> Create(Guid taskId, WriteTaskCommentRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activity, INotificationWriter notifications, ICurrentUser current, CancellationToken ct)
    {
        var task = await AccessibleTask(taskId, db, current, ct); if (task is null) return Problem("comment-forbidden", "No puedes comentar en esta tarea.", 403);
        var author = await db.Users.SingleOrDefaultAsync(x => x.Id == current.UserId, ct); if (author is null || !author.IsActive) return Problem("comment-forbidden", "No puedes comentar en esta tarea.", 403);
        try { var comment = TaskComment.Create(task.Id, current.UserId, request.Content ?? string.Empty, clock); db.TaskComments.Add(comment); activity.Record(task.ProjectId, task.Id, ActivityType.CommentAdded, new { commentId = comment.Id }); var assignees = await db.TaskAssignees.Where(x => x.TaskId == task.Id).Select(x => x.UserId).ToListAsync(ct); await notifications.WriteAsync(assignees, current.UserId, NotificationType.CommentAdded, task.ProjectId, task.Id, new { taskTitle = task.Title }, ct); await db.SaveChangesAsync(ct); return Results.Created($"/api/tasks/{task.Id}/comments/{comment.Id}", Response(comment, author)); }
        catch (ArgumentException) { return Problem("comment-validation", "El comentario debe tener entre 1 y 4000 caracteres.", 400); }
    }
    private static async Task<IResult> List(Guid taskId, string? cursor, int? limit, NakamaDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (await AccessibleTask(taskId, db, current, ct) is null) return Problem("comment-forbidden", "No puedes ver los comentarios de esta tarea.", 403);
        var position = Decode(cursor); if (cursor is not null && position is null) return Problem("comment-cursor-invalid", "El cursor no es válido.", 400);
        var query = from comment in db.TaskComments.AsNoTracking() join user in db.Users.AsNoTracking() on comment.AuthorUserId equals user.Id where comment.TaskId == taskId select new { comment, user };
        if (position is { } value) query = query.Where(x => x.comment.CreatedAt > value.CreatedAt || x.comment.CreatedAt == value.CreatedAt && x.comment.Id.CompareTo(value.Id) > 0);
        var rows = await query.OrderBy(x => x.comment.CreatedAt).ThenBy(x => x.comment.Id).Take(Math.Clamp(limit ?? 50, 1, 100) + 1).ToListAsync(ct);
        var pageSize = Math.Clamp(limit ?? 50, 1, 100); var page = rows.Take(pageSize).Select(x => Response(x.comment, x.user)).ToList(); var last = page.LastOrDefault();
        return Results.Ok(new TaskCommentFeedResponse(page, rows.Count > pageSize && last is not null ? Encode(last.CreatedAt, last.Id) : null));
    }
    private static async Task<IResult> Edit(Guid taskId, Guid commentId, WriteTaskCommentRequest request, NakamaDbContext db, IClock clock, IActivityRecorder activity, ICurrentUser current, CancellationToken ct)
    {
        var task = await AccessibleTask(taskId, db, current, ct); if (task is null) return Problem("comment-forbidden", "No puedes editar comentarios en esta tarea.", 403);
        var comment = await db.TaskComments.SingleOrDefaultAsync(x => x.Id == commentId && x.TaskId == taskId, ct); if (comment is null) return Problem("comment-not-found", "No existe el comentario.", 404); if (comment.AuthorUserId != current.UserId) return Problem("comment-forbidden", "Solo la persona autora puede editar este comentario.", 403);
        try { comment.Edit(request.Content ?? string.Empty, clock); activity.Record(task.ProjectId, task.Id, ActivityType.CommentEdited, new { commentId }); await db.SaveChangesAsync(ct); var author = await db.Users.SingleAsync(x => x.Id == comment.AuthorUserId, ct); return Results.Ok(Response(comment, author)); } catch (ArgumentException) { return Problem("comment-validation", "El comentario debe tener entre 1 y 4000 caracteres.", 400); }
    }
    private static async Task<IResult> Delete(Guid taskId, Guid commentId, NakamaDbContext db, IActivityRecorder activity, ICurrentUser current, CancellationToken ct)
    {
        var task = await AccessibleTask(taskId, db, current, ct); if (task is null) return Problem("comment-forbidden", "No puedes eliminar comentarios en esta tarea.", 403);
        var comment = await db.TaskComments.SingleOrDefaultAsync(x => x.Id == commentId && x.TaskId == taskId, ct); if (comment is null) return Problem("comment-not-found", "No existe el comentario.", 404); if (comment.AuthorUserId != current.UserId && current.Role != UserRole.Admin) return Problem("comment-forbidden", "No puedes eliminar este comentario.", 403);
        db.TaskComments.Remove(comment); activity.Record(task.ProjectId, task.Id, ActivityType.CommentDeleted, new { commentId }); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    private static TaskCommentResponse Response(TaskComment comment, User author) => new(comment.Id, comment.Content, new(author.Id, author.FullName), comment.CreatedAt, comment.UpdatedAt, comment.IsEdited);
    private static string Encode(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(createdAt, id))));
    private static Cursor? Decode(string? cursor) { if (string.IsNullOrWhiteSpace(cursor)) return null; try { return JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); } catch (Exception e) when (e is FormatException or JsonException) { return null; } }
    private sealed record Cursor(DateTimeOffset CreatedAt, Guid Id);
}
