using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Activity.Features;

public sealed record ActivityActorResponse(Guid Id, string FullName, string Email);
public sealed record ActivityResponse(Guid Id, Guid ProjectId, Guid? TaskId, string ActivityType, ActivityActorResponse Actor, DateTimeOffset OccurredAt, JsonElement? Metadata);
public sealed record ActivityFeedResponse(IReadOnlyList<ActivityResponse> Items, string? NextCursor);

internal static class ActivityEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tasks/{taskId:guid}/activity", (Guid taskId, string? cursor, int? limit, NakamaDbContext db, CancellationToken ct) => GetFeed(taskId, true, cursor, limit, db, ct)).WithTags("Activity").RequireAuthorization(Policies.AuthenticatedUser);
        app.MapGet("/api/projects/{projectId:guid}/activity", (Guid projectId, string? cursor, int? limit, NakamaDbContext db, CancellationToken ct) => GetFeed(projectId, false, cursor, limit, db, ct)).WithTags("Activity").RequireAuthorization(Policies.AuthenticatedUser);
    }

    private static async Task<IResult> GetFeed(Guid id, bool isTaskFeed, string? cursor, int? limit, NakamaDbContext db, CancellationToken ct)
    {
        var pageSize = Math.Clamp(limit ?? 50, 1, 100);
        var position = DecodeCursor(cursor);
        if (cursor is not null && position is null) return Results.Problem(type: "https://nakama/errors/activity-cursor-invalid", statusCode: 400);
        if (isTaskFeed && !await db.Tasks.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.Problem(type: "https://nakama/errors/task-not-found", statusCode: 404);
        if (!isTaskFeed && !await db.Projects.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.Problem(type: "https://nakama/errors/project-not-found", statusCode: 404);

        var query = from activity in db.ActivityLogs.AsNoTracking()
                    join user in db.Users.AsNoTracking() on activity.ActorUserId equals user.Id
                    where isTaskFeed ? activity.TaskId == id : activity.ProjectId == id
                    select new { activity, user };
        if (position is { } value)
            query = query.Where(x => x.activity.OccurredAt < value.OccurredAt || (x.activity.OccurredAt == value.OccurredAt && x.activity.Id.CompareTo(value.Id) < 0));
        var items = await query.OrderByDescending(x => x.activity.OccurredAt).ThenByDescending(x => x.activity.Id).Take(pageSize + 1).ToListAsync(ct);
        var hasNext = items.Count > pageSize;
        var page = items.Take(pageSize).Select(x => new ActivityResponse(x.activity.Id, x.activity.ProjectId, x.activity.TaskId, x.activity.ActivityType.ToString(), new(x.user.Id, x.user.FullName, x.user.Email), x.activity.OccurredAt, ParseMetadata(x.activity.MetadataJson))).ToList();
        var last = page.LastOrDefault();
        return Results.Ok(new ActivityFeedResponse(page, hasNext && last is not null ? EncodeCursor(last.OccurredAt, last.Id) : null));
    }

    private static JsonElement? ParseMetadata(string? metadataJson) => metadataJson is null ? null : JsonDocument.Parse(metadataJson).RootElement.Clone();
    private static string EncodeCursor(DateTimeOffset occurredAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(occurredAt, id))));
    private static Cursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try { return JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); }
        catch (FormatException) { return null; }
        catch (JsonException) { return null; }
    }

    private sealed record Cursor(DateTimeOffset OccurredAt, Guid Id);
}
