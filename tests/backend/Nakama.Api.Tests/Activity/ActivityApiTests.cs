using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Activity.Features;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Activity;

[Collection(PostgresCollection.Name)]
public sealed class ActivityApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Project_and_task_feeds_expose_immutable_activity_with_actor_and_metadata()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var actor = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        var user = await CreateUser(client, "activity@nakama.com");
        var project = await CreateProject(client, user.Id);
        var stage = await CreateStage(client, project.Id);
        var task = await CreateTask(client, project.Id, stage.Id, user.Id);

        var taskFeed = await client.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{task.Id}/activity");
        var taskCreated = Assert.Single(taskFeed!.Items);
        Assert.Equal("TaskCreated", taskCreated.ActivityType);
        Assert.Equal(actor!.Id, taskCreated.Actor.Id);
        Assert.Equal(task.Id, taskCreated.TaskId);
        Assert.True(taskCreated.Metadata!.Value.TryGetProperty("stageId", out _));

        var projectFeed = await client.GetFromJsonAsync<ActivityFeedResponse>($"/api/projects/{project.Id}/activity?limit=1");
        Assert.Single(projectFeed!.Items);
        Assert.NotNull(projectFeed.NextCursor);
        var secondPage = await client.GetFromJsonAsync<ActivityFeedResponse>($"/api/projects/{project.Id}/activity?cursor={Uri.EscapeDataString(projectFeed.NextCursor!)}&limit=100");
        Assert.Contains(secondPage!.Items, item => item.ActivityType == "ProjectCreated");
    }

    [PostgresFact]
    public async Task Failed_mutation_does_not_persist_activity()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var user = await CreateUser(client, "activity-failure@nakama.com");
        var project = await CreateProject(client, user.Id);
        var stage = await CreateStage(client, project.Id);
        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stage.Id, title = "", priority = "Medium"});
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var feed = await client.GetFromJsonAsync<ActivityFeedResponse>($"/api/projects/{project.Id}/activity?limit=100");
        Assert.DoesNotContain(feed!.Items, item => item.ActivityType == "TaskCreated");
    }

    [PostgresFact]
    public async Task Workflow_records_each_successful_transition_and_not_a_failed_transition()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var actor = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        var user = await CreateUser(client, "activity-workflow@nakama.com");
        var project = await CreateProject(client, user.Id);
        var stage = await CreateStage(client, project.Id);
        var task = await CreateTask(client, project.Id, stage.Id, user.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version })).StatusCode);
        task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version })).StatusCode);
        task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{task.Id}/request-changes", new { version = task.Version })).StatusCode);
        task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{task.Id}/cancel", new { version = task.Version })).StatusCode);
        var failed = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version + 1 });
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        var feed = await client.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{task.Id}/activity?limit=100");
        var types = feed!.Items.Select(item => item.ActivityType).ToArray();
        Assert.Contains("TaskStarted", types);
        Assert.Contains("TaskSubmittedForReview", types);
        Assert.Contains("TaskChangesRequested", types);
        Assert.Contains("TaskCancelled", types);
        Assert.All(feed.Items, item => Assert.Equal(actor!.Id, item.Actor.Id));
    }

    private static async Task<UserDetailResponse> CreateUser(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/users", new { fullName = email, email, role = "Collaborator", password = PostgresApiFactory.DefaultPassword });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!;
    }
    private static async Task<ProjectCreatedResponse> CreateProject(HttpClient client, Guid userId)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Activity project"});
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
    }
    private static async Task<StageResponse> CreateStage(HttpClient client, Guid projectId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/stages", new { name = "Activity stage" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StageResponse>())!;
    }
    private static async Task<TaskResponse> CreateTask(HttpClient client, Guid projectId, Guid stageId, Guid userId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { stageId, title = "Activity task", priority = "Medium"});
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }
}
