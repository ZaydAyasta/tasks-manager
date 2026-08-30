using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class SubtasksApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Create_complete_reopen_and_progress_preserve_task_workflow()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient();
        var user = await CreateUser(client, "subtasks-a@nakama.com"); var task = await CreateTask(client, user.Id);
        var first = await CreateSubtask(client, task.Id, user.Id, task.Version, "  First  ");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); var one = await first.Content.ReadFromJsonAsync<SubtaskResponse>();
        Assert.Equal("First", one!.Title); Assert.Equal(1, one.Position);
        var second = await CreateSubtask(client, task.Id, user.Id, one.TaskVersion!.Value, "Second"); var two = await second.Content.ReadFromJsonAsync<SubtaskResponse>();
        var complete = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks/{one.Id}/complete", new { userId = user.Id, taskVersion = two!.TaskVersion });
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
        var detail = await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}");
        Assert.Equal("Pending", detail!.Status); Assert.Equal(1, detail.SubtaskProgress.Completed); Assert.Equal(2, detail.SubtaskProgress.Total);
        var reopen = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks/{one.Id}/reopen", new { userId = user.Id, taskVersion = detail.Version });
        Assert.Equal(HttpStatusCode.NoContent, reopen.StatusCode);
        var list = await client.GetFromJsonAsync<List<SubtaskResponse>>($"/api/tasks/{task.Id}/subtasks");
        Assert.False(list![0].IsCompleted); Assert.Null(list[0].CompletedAt);
    }

    [PostgresFact]
    public async Task Delete_recompacts_positions_and_stale_version_conflicts()
    {
        await factory.ResetDatabaseAsync(); using var client = factory.CreateClient(); var user = await CreateUser(client, "subtasks-b@nakama.com"); var task = await CreateTask(client, user.Id);
        var a = await Read(CreateSubtask(client, task.Id, user.Id, task.Version, "A")); var b = await Read(CreateSubtask(client, task.Id, user.Id, a.TaskVersion!.Value, "B")); var c = await Read(CreateSubtask(client, task.Id, user.Id, b.TaskVersion!.Value, "C"));
        var stale = await client.PutAsJsonAsync($"/api/tasks/{task.Id}/subtasks/{c.Id}", new { title = "no", taskVersion = b.TaskVersion }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{task.Id}/subtasks/{b.Id}") { Content = JsonContent.Create(new { taskVersion = c.TaskVersion }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        var list = await client.GetFromJsonAsync<List<SubtaskResponse>>($"/api/tasks/{task.Id}/subtasks"); Assert.Collection(list!, x => Assert.Equal(1, x.Position), x => Assert.Equal(2, x.Position));
    }

    private static async Task<SubtaskResponse> Read(Task<HttpResponseMessage> response) => (await (await response).Content.ReadFromJsonAsync<SubtaskResponse>())!;
    private static Task<HttpResponseMessage> CreateSubtask(HttpClient client, Guid taskId, Guid userId, long version, string title) => client.PostAsJsonAsync($"/api/tasks/{taskId}/subtasks", new { title, createdByUserId = userId, taskVersion = version });
    private static async Task<UserDetailResponse> CreateUser(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/users", new { fullName = email, email, role = "Collaborator" }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!; }
    private static async Task<TaskResponse> CreateTask(HttpClient client, Guid userId) { var project = await client.PostAsJsonAsync("/api/projects", new { name = "P", createdByUserId = userId }); var p = (await project.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!; var stage = await client.PostAsJsonAsync($"/api/projects/{p.Id}/stages", new { name = "S" }); var s = (await stage.Content.ReadFromJsonAsync<StageResponse>())!; var task = await client.PostAsJsonAsync($"/api/projects/{p.Id}/tasks", new { stageId = s.Id, title = "T", priority = "Low", createdByUserId = userId }); return (await task.Content.ReadFromJsonAsync<TaskResponse>())!; }
}
