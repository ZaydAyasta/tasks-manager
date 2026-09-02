using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class TaskDependenciesApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Deep_cycle_is_rejected_while_a_dag_and_dependents_are_allowed()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var setup = await CreateProjectSetup(client, "dependency-graph@nakama.com");
        var a = await CreateTask(client, setup, "A");
        var b = await CreateTask(client, setup, "B");
        var c = await CreateTask(client, setup, "C");

        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, b, a.Id, setup.User.Id)).StatusCode);
        b = await GetTask(client, b.Id);
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, c, b.Id, setup.User.Id)).StatusCode);
        var dependents = await client.GetFromJsonAsync<List<DependentResponse>>($"/api/tasks/{a.Id}/dependents");
        var dependentItems = Assert.IsType<List<DependentResponse>>(dependents);
        Assert.Single(dependentItems);
        Assert.Equal(b.Id, dependentItems[0].Task.Id);

        a = await GetTask(client, a.Id);
        var cycle = await AddDependency(client, a, c.Id, setup.User.Id);
        Assert.Equal(HttpStatusCode.Conflict, cycle.StatusCode);
        Assert.Equal("https://nakama/errors/task-dependency-cycle", await ProblemType(cycle));
    }

    [PostgresFact]
    public async Task Completed_prerequisite_enables_workflow_but_cancelled_prerequisite_does_not()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var setup = await CreateProjectSetup(client, "dependency-workflow@nakama.com");
        var prerequisite = await CreateTask(client, setup, "Prerequisite");
        var dependent = await CreateTask(client, setup, "Dependent");
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, dependent, prerequisite.Id, setup.User.Id)).StatusCode);

        dependent = await GetTask(client, dependent.Id);
        var blockedStart = await client.PostAsJsonAsync($"/api/tasks/{dependent.Id}/start", new { version = dependent.Version });
        Assert.Equal(HttpStatusCode.Conflict, blockedStart.StatusCode);
        Assert.Equal("https://nakama/errors/task-dependencies-not-satisfied", await ProblemType(blockedStart));

        await Complete(client, prerequisite.Id);
        dependent = await GetTask(client, dependent.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{dependent.Id}/start", new { version = dependent.Version })).StatusCode);
        dependent = await GetTask(client, dependent.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{dependent.Id}/submit-review", new { version = dependent.Version })).StatusCode);
        dependent = await GetTask(client, dependent.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{dependent.Id}/complete", new { version = dependent.Version })).StatusCode);

        var cancelled = await CreateTask(client, setup, "Cancelled");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{cancelled.Id}/cancel", new { version = cancelled.Version })).StatusCode);
        var blockedByCancelled = await CreateTask(client, setup, "Blocked by cancelled");
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, blockedByCancelled, cancelled.Id, setup.User.Id)).StatusCode);
        blockedByCancelled = await GetTask(client, blockedByCancelled.Id);
        var cancelledStart = await client.PostAsJsonAsync($"/api/tasks/{blockedByCancelled.Id}/start", new { version = blockedByCancelled.Version });
        Assert.Equal(HttpStatusCode.Conflict, cancelledStart.StatusCode);
    }

    [PostgresFact]
    public async Task Detail_and_project_list_report_dependency_progress_delete_and_concurrency()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var setup = await CreateProjectSetup(client, "dependency-progress@nakama.com");
        var completed = await CreateTask(client, setup, "Completed prerequisite");
        await Complete(client, completed.Id);
        var pending = await CreateTask(client, setup, "Pending prerequisite");
        var dependent = await CreateTask(client, setup, "Dependent");
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, dependent, completed.Id, setup.User.Id)).StatusCode);
        dependent = await GetTask(client, dependent.Id);
        var createSecond = await AddDependency(client, dependent, pending.Id, setup.User.Id);
        Assert.Equal(HttpStatusCode.Created, createSecond.StatusCode);

        dependent = await GetTask(client, dependent.Id);
        Assert.Equal(1, dependent.DependencyProgress.Satisfied);
        Assert.Equal(2, dependent.DependencyProgress.Total);
        Assert.False(dependent.DependenciesSatisfied);
        Assert.Equal(1, dependent.PendingDependencyCount);
        var list = await client.GetFromJsonAsync<List<TaskResponse>>($"/api/projects/{setup.Project.Id}/tasks");
        var listed = Assert.Single(list!, x => x.Id == dependent.Id);
        Assert.Equal(1, listed.PendingDependencyCount);

        var dependencies = await client.GetFromJsonAsync<List<DependencyResponse>>($"/api/tasks/{dependent.Id}/dependencies");
        var staleVersion = dependent.Version;
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{dependent.Id}/dependencies/{dependencies![0].Id}") { Content = JsonContent.Create(new { taskVersion = dependent.Version }) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        var staleDelete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{dependent.Id}/dependencies/{dependencies[1].Id}") { Content = JsonContent.Create(new { taskVersion = staleVersion }) };
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(staleDelete)).StatusCode);
    }

    private static async Task<ProjectSetup> CreateProjectSetup(HttpClient client, string email)
    {
        var userResponse = await client.PostAsJsonAsync("/api/users", new { fullName = email, email, role = "Collaborator", password = PostgresApiFactory.DefaultPassword });
        var user = (await userResponse.Content.ReadFromJsonAsync<UserDetailResponse>())!;
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Dependencies"});
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
        var stageResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Stage" });
        var stage = (await stageResponse.Content.ReadFromJsonAsync<StageResponse>())!;
        return new ProjectSetup(user, project, stage);
    }

    private static async Task<TaskResponse> CreateTask(HttpClient client, ProjectSetup setup, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{setup.Project.Id}/tasks", new { stageId = setup.Stage.Id, title, priority = "Medium"});
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }

    private static Task<HttpResponseMessage> AddDependency(HttpClient client, TaskResponse task, Guid prerequisiteId, Guid userId) =>
        client.PostAsJsonAsync($"/api/tasks/{task.Id}/dependencies", new { dependsOnTaskId = prerequisiteId, taskVersion = task.Version });
    private static async Task<TaskResponse> GetTask(HttpClient client, Guid taskId) => (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{taskId}"))!;
    private static async Task Complete(HttpClient client, Guid taskId)
    {
        var task = await GetTask(client, taskId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/start", new { version = task.Version })).StatusCode);
        task = await GetTask(client, taskId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/submit-review", new { version = task.Version })).StatusCode);
        task = await GetTask(client, taskId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new { version = task.Version })).StatusCode);
    }

    private static async Task<string?> ProblemType(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Type;
    private sealed record ProjectSetup(UserDetailResponse User, ProjectCreatedResponse Project, StageResponse Stage);
    private sealed record DependentResponse(Guid DependencyId, DependencyTaskResponse Task);
}
