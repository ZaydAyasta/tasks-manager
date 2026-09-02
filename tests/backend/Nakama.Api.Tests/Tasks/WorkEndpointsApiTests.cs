using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class WorkEndpointsApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Requires_a_token_and_returns_empty_items_for_an_assignee_without_work()
    {
        await factory.ResetDatabaseAsync();
        using var anonymous = factory.CreateAnonymousClient();
        using var admin = await factory.CreateAdminClientAsync();
        using var collaborator = await factory.CreateCollaboratorClientAsync(admin, "empty-work@nakama.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/work")).StatusCode);
        var work = await collaborator.GetFromJsonAsync<WorkFeedResponse>("/api/me/work");
        Assert.NotNull(work);
        Assert.Empty(work.Items);
        Assert.Null(work.NextCursor);
    }

    [PostgresFact]
    public async Task Returns_only_current_assignee_non_terminal_work_with_aggregate_context()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var member = await factory.CreateUserAsync(admin, "work-member@nakama.test");
        var other = await factory.CreateUserAsync(admin, "work-other@nakama.test");
        var setup = await CreateSetup(admin, member.Id);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/projects/{setup.Project.Id}/members", new { userId = other.Id })).StatusCode);
        var task = await CreateTask(admin, setup, "Critical work", "Critical", member.Id);
        var otherTask = await CreateTask(admin, setup, "Other work", "High", other.Id);
        var completed = await CreateTask(admin, setup, "Done", "High", member.Id);
        await Complete(admin, completed.Id);
        var cancelled = await CreateTask(admin, setup, "Cancelled", "Medium", member.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/tasks/{cancelled.Id}/cancel", new { version = cancelled.Version })).StatusCode);
        using var memberClient = await factory.CreateAuthenticatedClientAsync(member.Email, PostgresApiFactory.DefaultPassword);
        var subtask = await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks", new { title = "Checklist", taskVersion = task.Version });
        var createdSubtask = await subtask.Content.ReadFromJsonAsync<SubtaskResponse>();
        Assert.Equal(HttpStatusCode.Created, subtask.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks/{createdSubtask!.Id}/complete", new { taskVersion = createdSubtask.TaskVersion })).StatusCode);
        task = (await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        var prerequisite = await CreateTask(admin, setup, "Cancelled prerequisite", "Low", member.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/tasks/{prerequisite.Id}/cancel", new { version = prerequisite.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/dependencies", new { dependsOnTaskId = prerequisite.Id, taskVersion = task.Version })).StatusCode);
        task = (await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal(HttpStatusCode.Created, (await memberClient.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers", new { type = "TechnicalIssue", description = "Blocked", version = task.Version })).StatusCode);

        var work = await memberClient.GetFromJsonAsync<WorkFeedResponse>("/api/me/work");
        var item = Assert.Single(work!.Items, current => current.Id == task.Id);
        Assert.DoesNotContain(work.Items, current => current.Id == otherTask.Id || current.Id == completed.Id || current.Id == cancelled.Id);
        Assert.Equal(setup.Project.Id, item.Project.Id);
        Assert.Equal(setup.Project.Name, item.Project.Name);
        Assert.Equal(setup.Stage.Id, item.Stage.Id);
        Assert.Equal(setup.Stage.Name, item.Stage.Name);
        Assert.Equal(1, item.SubtaskProgress.Completed);
        Assert.Equal(1, item.SubtaskProgress.Total);
        Assert.Equal(1, item.PendingDependencyCount);
        Assert.Equal(1, item.ActiveBlockerCount);
    }

    [PostgresFact]
    public async Task Filters_orders_and_pages_without_duplicate_items()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var member = await factory.CreateUserAsync(admin, "work-pagination@nakama.test");
        var firstSetup = await CreateSetup(admin, member.Id, "A project", "A stage");
        var secondSetup = await CreateSetup(admin, member.Id, "B project", "B stage");
        var critical = await CreateTask(admin, firstSetup, "Critical", "Critical", member.Id, "2026-02-02T00:00:00Z");
        var high = await CreateTask(admin, firstSetup, "High", "High", member.Id, "2026-01-01T00:00:00Z");
        var pending = await CreateTask(admin, secondSetup, "Pending", "Medium", member.Id, null);
        using var memberClient = await factory.CreateAuthenticatedClientAsync(member.Email, PostgresApiFactory.DefaultPassword);

        var first = await memberClient.GetFromJsonAsync<WorkFeedResponse>("/api/me/work?limit=2");
        Assert.Equal(new[] { critical.Id, high.Id }, first!.Items.Select(item => item.Id));
        Assert.NotNull(first.NextCursor);
        var second = await memberClient.GetFromJsonAsync<WorkFeedResponse>($"/api/me/work?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Equal(new[] { pending.Id }, second!.Items.Select(item => item.Id));
        Assert.Null(second.NextCursor);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        var byProject = await memberClient.GetFromJsonAsync<WorkFeedResponse>($"/api/me/work?projectId={secondSetup.Project.Id}");
        Assert.Single(byProject!.Items, item => item.Id == pending.Id);
        var byStatus = await memberClient.GetFromJsonAsync<WorkFeedResponse>("/api/me/work?status=Pending");
        Assert.All(byStatus!.Items, item => Assert.Equal("Pending", item.Status));
    }

    [PostgresFact]
    public async Task Rejects_a_deactivated_current_user()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync(admin, "inactive-work@nakama.test");
        using var collaborator = await factory.CreateAuthenticatedClientAsync(user.Email, PostgresApiFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/users/{user.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await collaborator.GetAsync("/api/me/work")).StatusCode);
    }

    private static async Task<Setup> CreateSetup(HttpClient admin, Guid memberId, string projectName = "Work project", string stageName = "Work stage")
    {
        var project = (await (await admin.PostAsJsonAsync("/api/projects", new { name = projectName })).Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = memberId })).StatusCode);
        var stage = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = stageName })).Content.ReadFromJsonAsync<StageResponse>())!;
        return new(project, stage);
    }
    private static async Task<TaskResponse> CreateTask(HttpClient admin, Setup setup, string title, string priority, Guid userId, string? dueDate = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/projects/{setup.Project.Id}/tasks", new { stageId = setup.Stage.Id, title, priority, dueDate, assigneeIds = new[] { userId } });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }
    private static async Task Complete(HttpClient client, Guid taskId)
    {
        var task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{taskId}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/start", new { version = task.Version })).StatusCode);
        task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{taskId}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/submit-review", new { version = task.Version })).StatusCode);
        task = (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{taskId}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new { version = task.Version })).StatusCode);
    }
    private sealed record Setup(ProjectCreatedResponse Project, StageResponse Stage);
}
