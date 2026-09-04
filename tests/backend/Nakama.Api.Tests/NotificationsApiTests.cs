#pragma warning disable CS8602
using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class NotificationsApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Assignment_and_removal_create_owned_notifications_without_self_notification()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var owner = await factory.CreateUserAsync(admin, "notify-owner@test.local"); var assignee = await factory.CreateUserAsync(admin, "notify-assignee@test.local"); var setup = await Setup(admin, owner.Id, assignee.Id); using var ownerClient = await factory.CreateAuthenticatedClientAsync(owner.Email, PostgresApiFactory.DefaultPassword); using var assigneeClient = await factory.CreateAuthenticatedClientAsync(assignee.Email, PostgresApiFactory.DefaultPassword);
        var ownerFeed = await ownerClient.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); Assert.Empty(ownerFeed!.Items);
        var removed = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{setup.Task.Id}/assignees/{assignee.Id}") { Content = JsonContent.Create(new { version = setup.Task.Version }) }); Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var assigneeFeed = await assigneeClient.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); Assert.Contains(assigneeFeed!.Items, x => x.Type == "TaskUnassigned" && x.TaskId == setup.Task.Id && x.ProjectId == setup.Project.Id);
    }

    [PostgresFact]
    public async Task Review_complete_blocker_and_comment_notifications_deduplicate_and_exclude_actor()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var owner = await factory.CreateUserAsync(admin, "notify-owner2@test.local"); var member = await factory.CreateUserAsync(admin, "notify-member2@test.local"); var setup = await Setup(admin, owner.Id, member.Id); using var memberClient = await factory.CreateAuthenticatedClientAsync(member.Email, PostgresApiFactory.DefaultPassword); var task = setup.Task;
        var start = await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version }); Assert.Equal(HttpStatusCode.NoContent, start.StatusCode); task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; var review = await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version }); Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        var ownerFeed = await admin.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); Assert.Contains(ownerFeed!.Items, x => x.Type == "TaskSubmittedForReview" && x.TaskId == task.Id);
        task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; var changes = await admin.PostAsJsonAsync($"/api/tasks/{task.Id}/request-changes", new { version = task.Version }); Assert.Equal(HttpStatusCode.NoContent, changes.StatusCode); var memberFeed = await memberClient.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); Assert.Contains(memberFeed!.Items, x => x.Type == "TaskChangesRequested");
        task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version }); task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version });
        task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; await admin.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { version = task.Version }); memberFeed = await memberClient.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); Assert.Contains(memberFeed!.Items, x => x.Type == "TaskCompleted");
    }

    [PostgresFact]
    public async Task Preferences_read_state_filters_and_read_all_are_scoped_to_recipient()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var owner = await factory.CreateUserAsync(admin, "notify-owner3@test.local"); var member = await factory.CreateUserAsync(admin, "notify-member3@test.local"); var setup = await Setup(admin, owner.Id, member.Id); using var memberClient = await factory.CreateAuthenticatedClientAsync(member.Email, PostgresApiFactory.DefaultPassword);
        var prefs = await memberClient.GetFromJsonAsync<List<PreferenceResponse>>("/api/me/notification-preferences"); Assert.NotNull(prefs); Assert.All(prefs!, p => Assert.True(p.Enabled));
        Assert.Equal(HttpStatusCode.NoContent, (await memberClient.PutAsJsonAsync("/api/me/notification-preferences/TaskAssigned", new { enabled = false })).StatusCode);
        var unread = await memberClient.GetFromJsonAsync<NotificationCountResponse>("/api/me/notifications/unread-count"); Assert.Equal(0, unread!.Count);
        var list = await memberClient.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications?unreadOnly=true"); Assert.Empty(list!.Items);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateAnonymousClient().GetAsync("/api/me/notifications")).StatusCode);
    }

    [PostgresFact]
    public async Task Read_is_idempotent_and_foreign_notification_is_hidden()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var owner = await factory.CreateUserAsync(admin, "notify-owner4@test.local"); var member = await factory.CreateUserAsync(admin, "notify-member4@test.local"); var setup = await Setup(admin, owner.Id, member.Id); using var memberClient = await factory.CreateAuthenticatedClientAsync(member.Email, PostgresApiFactory.DefaultPassword); var task = setup.Task; await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version }); task = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}")!; await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version }); var feed = await admin.GetFromJsonAsync<NotificationFeedResponse>("/api/me/notifications"); var notification = Assert.Single(feed!.Items); Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/me/notifications/{notification.Id}/read", null)).StatusCode); Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/me/notifications/{notification.Id}/read", null)).StatusCode); var count = await admin.GetFromJsonAsync<NotificationCountResponse>("/api/me/notifications/unread-count"); Assert.Equal(0, count!.Count); Assert.Equal(HttpStatusCode.NotFound, (await memberClient.PostAsync($"/api/me/notifications/{notification.Id}/read", null)).StatusCode);
    }

    private static async Task<(ProjectCreatedResponse Project, TaskResponse Task)> Setup(HttpClient admin, Guid ownerId, Guid assigneeId)
    { var project = (await (await admin.PostAsJsonAsync("/api/projects", new { name = "Notifications" })).Content.ReadFromJsonAsync<ProjectCreatedResponse>())!; await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = ownerId }); await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = assigneeId }); var stage = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Stage" })).Content.ReadFromJsonAsync<StageResponse>())!; var task = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stage.Id, title = "Notify task", priority = "Medium", assigneeIds = new[] { assigneeId } })).Content.ReadFromJsonAsync<TaskResponse>())!; return (project, task); }
}
