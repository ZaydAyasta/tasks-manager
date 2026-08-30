using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class TaskBlockersApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Reports_multiple_blockers_and_restores_after_last_resolution()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var owner = await User(client, "blocker-owner@nakama.com"); var project = await Project(client, owner.Id); var stage = await Stage(client, project.Id); var task = await Task(client, project.Id, stage.Id, owner.Id);
        var first = await Report(client, task.Id, owner.Id, task.Version, "Approval");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); var blockerA = await first.Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var second = await Report(client, task.Id, owner.Id, blockerA!.TaskVersion!.Value, "TechnicalIssue"); var blockerB = await second.Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); Assert.Equal("Blocked", detail!.Status);
        var resolvedA = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers/{blockerA.Id}/resolve", new { resolvedByUserId = owner.Id, version = blockerB!.TaskVersion });
        Assert.Equal(HttpStatusCode.OK, resolvedA.StatusCode); detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); Assert.Equal("Blocked", detail!.Status);
        var resolveA = await resolvedA.Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var resolvedB = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers/{blockerB.Id}/resolve", new { resolvedByUserId = owner.Id, version = resolveA!.TaskVersion });
        Assert.Equal(HttpStatusCode.OK, resolvedB.StatusCode); detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); Assert.Equal("Pending", detail!.Status);
        var history = await client.GetFromJsonAsync<List<TaskBlockerResponse>>($"/api/tasks/{task.Id}/blockers"); Assert.Equal(2, history!.Count); Assert.All(history, blocker => Assert.True(blocker.IsResolved));
    }
    [PostgresFact]
    public async Task Rejects_terminal_report_and_keeps_cancelled_task_cancelled_when_resolving()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var owner = await User(client, "cancel-owner@nakama.com"); var project = await Project(client, owner.Id); var stage = await Stage(client, project.Id); var task = await Task(client, project.Id, stage.Id, owner.Id);
        var report = await Report(client, task.Id, owner.Id, task.Version, "External"); var blocker = await report.Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); var cancel = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/cancel", new { version = detail!.Version }); Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); var resolve = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers/{blocker!.Id}/resolve", new { resolvedByUserId = owner.Id, version = detail!.Version }); Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"); Assert.Equal("Cancelled", detail!.Status);
        var postCancel = await Report(client, task.Id, owner.Id, detail.Version, "Other"); Assert.Equal(HttpStatusCode.Conflict, postCancel.StatusCode);
    }
    [PostgresFact]
    public async Task Concurrent_resolution_with_a_stale_task_version_is_rejected()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var owner = await User(client, "concurrency-owner@nakama.com"); var project = await Project(client, owner.Id); var stage = await Stage(client, project.Id); var task = await Task(client, project.Id, stage.Id, owner.Id);
        var first = await (await Report(client, task.Id, owner.Id, task.Version, "Approval")).Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var second = await (await Report(client, task.Id, owner.Id, first!.TaskVersion!.Value, "Other")).Content.ReadFromJsonAsync<TaskBlockerResponse>();
        var sharedVersion = second!.TaskVersion!.Value;
        var winner = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers/{first.Id}/resolve", new { resolvedByUserId = owner.Id, version = sharedVersion });
        var loser = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers/{second.Id}/resolve", new { resolvedByUserId = owner.Id, version = sharedVersion });
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode); Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
    }
    private static Task<HttpResponseMessage> Report(HttpClient client, Guid taskId, Guid userId, long version, string type) => client.PostAsJsonAsync($"/api/tasks/{taskId}/blockers", new { type, description = "  Waiting  ", reportedByUserId = userId, version });
    private static async Task<UserDetailResponse> User(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/users", new { fullName = email, email, role = "Collaborator" }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!; }
    private static async Task<ProjectCreatedResponse> Project(HttpClient client, Guid id) { var response = await client.PostAsJsonAsync("/api/projects", new { name = "Project", createdByUserId = id }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!; }
    private static async Task<StageResponse> Stage(HttpClient client, Guid id) { var response = await client.PostAsJsonAsync($"/api/projects/{id}/stages", new { name = "Stage" }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<StageResponse>())!; }
    private static async Task<TaskResponse> Task(HttpClient client, Guid projectId, Guid stageId, Guid userId) { var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { stageId, title = "Task", priority = "Medium", createdByUserId = userId }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<TaskResponse>())!; }
}
