using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class TaskAssignmentAccessApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Unassigned_member_can_collaborate_but_cannot_change_work_until_assigned()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync(admin, "assignment@trello.local");
        var project = (await (await admin.PostAsJsonAsync("/api/projects", new { name = "Assignments", description = "Description for both lists" })).Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = user.Id })).StatusCode);
        var stage = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Build" })).Content.ReadFromJsonAsync<StageResponse>())!;
        var task = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stage.Id, title = "Assigned work", priority = "Medium" })).Content.ReadFromJsonAsync<TaskResponse>())!;
        using var member = await factory.CreateAuthenticatedClientAsync(user.Email, PostgresApiFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks", new { title = "Denied", taskVersion = task.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers", new { type = "TechnicalIssue", description = "Denied", version = task.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/dependencies", new { dependsOnTaskId = Guid.NewGuid(), taskVersion = task.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/comments", new { content = "Project members can discuss work." })).StatusCode);
        var projects = (await member.GetFromJsonAsync<List<ProjectListItemResponse>>($"/api/users/{user.Id}/projects"))!;
        Assert.Equal("Description for both lists", Assert.Single(projects).Description);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/tasks/{task.Id}/assignees", new { userId = user.Id, version = task.Version })).StatusCode);
        task = (await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version })).StatusCode);
        task = (await member.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        Assert.Equal("InProgress", task.Status);
        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks", new { title = "Allowed after assignment", taskVersion = task.Version })).StatusCode);
    }
}
