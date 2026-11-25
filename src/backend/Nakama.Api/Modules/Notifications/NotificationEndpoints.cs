using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Notifications.Domain;

namespace Nakama.Api.Modules.Notifications;

public sealed record NotificationActorResponse(Guid Id, string FullName);
public sealed record NotificationResponse(Guid Id, string Type, bool IsRead, DateTimeOffset CreatedAt, NotificationActorResponse? Actor, Guid? ProjectId, Guid? TaskId, JsonElement? Metadata);
public sealed record NotificationFeedResponse(IReadOnlyList<NotificationResponse> Items, string? NextCursor);
public sealed record NotificationCountResponse(int Count);
public sealed record PreferenceResponse(string Type, bool Enabled);
public sealed record UpdatePreferenceRequest(bool Enabled);

internal static class NotificationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").RequireAuthorization(Policies.AuthenticatedUser);
        group.MapGet("/notifications", List);
        group.MapGet("/notifications/unread-count", Count);
        group.MapPost("/notifications/{id:guid}/read", Read);
        group.MapPost("/notifications/read-all", ReadAll);
        group.MapGet("/notification-preferences", Preferences);
        group.MapPut("/notification-preferences/{type}", UpdatePreference);
    }

    private static async Task<IResult> List(bool? unreadOnly, string? cursor, int? limit, NakamaDbContext db, ICurrentUser me, CancellationToken ct)
    {
        var position = Decode(cursor);
        if (cursor is not null && position is null)
            return Results.Problem(title: "El cursor no es válido.", statusCode: 400);
        var query = from notification in db.Notifications.AsNoTracking()
                    join user in db.Users.AsNoTracking() on notification.ActorUserId equals user.Id into actors
                    from actor in actors.DefaultIfEmpty()
                    where notification.RecipientUserId == me.UserId && (unreadOnly != true || notification.ReadAt == null)
                    select new { notification, actor };
        if (position is { } after)
            query = query.Where(x => x.notification.CreatedAt < after.CreatedAt ||
                x.notification.CreatedAt == after.CreatedAt && x.notification.Id.CompareTo(after.Id) < 0);
        var pageSize = Math.Clamp(limit ?? 50, 1, 100);
        var rows = await query.OrderByDescending(x => x.notification.CreatedAt).ThenByDescending(x => x.notification.Id).Take(pageSize + 1).ToListAsync(ct);
        var page = rows.Take(pageSize).Select(x => new NotificationResponse(x.notification.Id, x.notification.Type.ToString(),
            x.notification.IsRead, x.notification.CreatedAt, x.actor is null ? null : new(x.actor.Id, x.actor.FullName),
            x.notification.ProjectId, x.notification.TaskId, Metadata(x.notification.MetadataJson))).ToList();
        var last = page.LastOrDefault();
        return Results.Ok(new NotificationFeedResponse(page, rows.Count > pageSize && last is not null ? Encode(new(last.CreatedAt, last.Id)) : null));
    }

    private static JsonElement? Metadata(string? json)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<IResult> Count(NakamaDbContext db, ICurrentUser me, CancellationToken ct) =>
        Results.Ok(new NotificationCountResponse(await db.Notifications.CountAsync(x => x.RecipientUserId == me.UserId && x.ReadAt == null, ct)));

    private static async Task<IResult> Read(Guid id, NakamaDbContext db, IClock clock, ICurrentUser me, CancellationToken ct)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientUserId == me.UserId, ct);
        if (notification is null) return Results.NotFound();
        notification.MarkRead(clock);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReadAll(NakamaDbContext db, IClock clock, ICurrentUser me, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.Notifications.Where(x => x.RecipientUserId == me.UserId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, now), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Preferences(NakamaDbContext db, ICurrentUser me, CancellationToken ct)
    {
        var preferences = await db.NotificationPreferences.Where(x => x.UserId == me.UserId).ToDictionaryAsync(x => x.Type, x => x.IsEnabled, ct);
        return Results.Ok(Enum.GetValues<NotificationType>().Select(type => new PreferenceResponse(type.ToString(), preferences.GetValueOrDefault(type, true))));
    }

    private static async Task<IResult> UpdatePreference(string type, UpdatePreferenceRequest request, NakamaDbContext db, ICurrentUser me, CancellationToken ct)
    {
        if (!Enum.TryParse<NotificationType>(type, false, out var value) || !Enum.IsDefined(value)) return Results.BadRequest();
        var preference = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == me.UserId && x.Type == value, ct);
        if (preference is null) db.NotificationPreferences.Add(NotificationPreference.Create(me.UserId, value, request.Enabled));
        else preference.Set(request.Enabled);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private sealed record Cursor(DateTimeOffset CreatedAt, Guid Id);
    private static string Encode(Cursor value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
    private static Cursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 512) return null;
        try
        {
            var value = JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            return value is { Id: var id } && id != Guid.Empty && value.CreatedAt != default ? value : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { return null; }
    }
}
