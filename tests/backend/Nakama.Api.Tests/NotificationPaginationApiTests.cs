using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Notifications.Domain;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class NotificationPaginationApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Cursor_traverses_equal_timestamps_without_gaps_and_never_exposes_foreign_notifications()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var me = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        var other = await factory.CreateUserAsync(admin, "pagination-other@trello.local");
        var ownedIds = new List<Guid>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
            var clock = new FixedClock();
            for (var i = 0; i < 5; i++)
            {
                var notification = Notification.Create(me.Id, other.Id, NotificationType.TaskAssigned, null, null, null, clock);
                db.Notifications.Add(notification); ownedIds.Add(notification.Id);
            }
            db.Notifications.Add(Notification.Create(other.Id, me.Id, NotificationType.TaskAssigned, null, null, null, clock));
            await db.SaveChangesAsync();
        }
        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var feed = (await admin.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications?limit=2" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor))))!;
            Assert.InRange(feed.Items.Count, 1, 2);
            seen.AddRange(feed.Items.Select(item => item.Id)); cursor = feed.NextCursor;
            Assert.True(seen.Count <= 5, "Cursor must advance.");
        } while (cursor is not null);
        Assert.Equal(5, seen.Distinct().Count());
        Assert.Equal(ownedIds.Order(), seen.Order());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/me/notifications?cursor=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/me/notification-preferences/99", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/me/notifications/read-all", null)).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications?unreadOnly=true"))!.Items);
        using var outsider = await factory.CreateAuthenticatedClientAsync(other.Email, PostgresApiFactory.DefaultPassword);
        Assert.Single((await outsider.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications?unreadOnly=true"))!.Items);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero); }
}
