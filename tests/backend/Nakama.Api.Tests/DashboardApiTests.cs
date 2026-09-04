using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Dashboard.Features;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class DashboardApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Dashboard_requires_an_admin_and_returns_zeroes_for_an_empty_database()
    {
        await factory.ResetDatabaseAsync();
        using var anonymous = factory.CreateAnonymousClient();
        using var admin = await factory.CreateAdminClientAsync();
        using var collaborator = await factory.CreateCollaboratorClientAsync(admin, "dashboard-collaborator@nakama.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await collaborator.GetAsync("/api/admin/dashboard")).StatusCode);

        var dashboard = await admin.GetFromJsonAsync<AdminDashboardResponse>("/api/admin/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(0, dashboard.Summary.ActiveProjects);
        Assert.Equal(0, dashboard.Summary.ActiveTasks);
        Assert.Equal(0, dashboard.Summary.BlockedTasks);
        Assert.Equal(0, dashboard.Summary.InReviewTasks);
        Assert.Equal(0, dashboard.Summary.OverdueTasks);
        Assert.Equal(0, dashboard.Summary.CriticalTasks);
        Assert.Empty(dashboard.Projects);
        Assert.Empty(dashboard.Attention);
    }

    [PostgresFact]
    public async Task Dashboard_keeps_a_project_without_tasks_at_zero_progress()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();

        var createProject = await admin.PostAsJsonAsync("/api/projects", new { name = "Proyecto sin tareas" });
        createProject.EnsureSuccessStatusCode();
        var createdProject = (await createProject.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;

        var dashboard = (await admin.GetFromJsonAsync<AdminDashboardResponse>("/api/admin/dashboard"))!;
        var project = Assert.Single(dashboard.Projects);

        Assert.Equal(createdProject.Id, project.Id);
        Assert.Equal(1, dashboard.Summary.ActiveProjects);
        Assert.Equal(0, dashboard.Summary.ActiveTasks);
        Assert.Equal(0, project.TaskProgress.Completed);
        Assert.Equal(0, project.TaskProgress.Total);
        Assert.Equal(0, project.BlockedCount);
        Assert.Equal(0, project.InReviewCount);
        Assert.Equal(0, project.OverdueCount);
    }

    [PostgresFact]
    public async Task Dashboard_aggregates_project_progress_non_terminal_metrics_and_priority_ordered_attention()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var adminUser = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        var assignee = await factory.CreateUserAsync(admin, "dashboard-assignee@nakama.test", fullName: "Ana Responsable");
        var setup = await CreateSetupAsync(admin, assignee.Id);

        var blockedCritical = await CreateTaskAsync(admin, setup, "Bloqueada crítica", "Critical", "2020-01-01T00:00:00Z", [assignee.Id, adminUser.Id]);
        await BlockAsync(admin, blockedCritical);

        var overdue = await CreateTaskAsync(admin, setup, "Vencida", "High", "2020-01-02T00:00:00Z", [assignee.Id]);
        var review = await CreateTaskAsync(admin, setup, "Esperando revisión", "Medium", "2099-01-01T00:00:00Z", [assignee.Id]);
        await SubmitForReviewAsync(admin, review.Id);

        var critical = await CreateTaskAsync(admin, setup, "Crítica activa", "Critical", "2099-01-02T00:00:00Z", [assignee.Id]);
        var completed = await CreateTaskAsync(admin, setup, "Completada", "High", null, [assignee.Id]);
        await CompleteAsync(admin, completed.Id);

        var cancelled = await CreateTaskAsync(admin, setup, "Cancelada", "High", null, [assignee.Id]);
        await CancelAsync(admin, cancelled.Id);

        var dashboard = (await admin.GetFromJsonAsync<AdminDashboardResponse>("/api/admin/dashboard"))!;
        var project = Assert.Single(dashboard.Projects);
        var attention = dashboard.Attention;

        Assert.Equal(1, dashboard.Summary.ActiveProjects);
        Assert.Equal(4, dashboard.Summary.ActiveTasks);
        Assert.Equal(1, dashboard.Summary.BlockedTasks);
        Assert.Equal(1, dashboard.Summary.InReviewTasks);
        Assert.Equal(2, dashboard.Summary.OverdueTasks);
        Assert.Equal(2, dashboard.Summary.CriticalTasks);

        Assert.Equal(setup.Project.Id, project.Id);
        Assert.Equal(1, project.TaskProgress.Completed);
        Assert.Equal(6, project.TaskProgress.Total);
        Assert.Equal(1, project.BlockedCount);
        Assert.Equal(1, project.InReviewCount);
        Assert.Equal(2, project.OverdueCount);

        Assert.Equal(blockedCritical.Id, attention[0].TaskId);
        Assert.Equal(overdue.Id, attention[1].TaskId);
        Assert.Equal(review.Id, attention[2].TaskId);
        Assert.Equal(critical.Id, attention[3].TaskId);
        Assert.DoesNotContain(attention, item => item.TaskId == completed.Id || item.TaskId == cancelled.Id);
        Assert.Equal(new[] { adminUser.Id, assignee.Id }.Order(), attention[0].Assignees.Select(item => item.Id).Order());
        Assert.All(attention, item => Assert.DoesNotContain(item.Assignees, assigneeItem => assigneeItem.FullName.Contains('@')));
    }

    [PostgresFact]
    public async Task Dashboard_limits_attention_without_per_task_request_shapes()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var adminUser = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        var setup = await CreateSetupAsync(admin, adminUser.Id);

        for (var index = 0; index < 16; index++)
        {
            await CreateTaskAsync(admin, setup, $"Crítica {index:D2}", "Critical", "2099-01-01T00:00:00Z", [adminUser.Id]);
        }

        var dashboard = (await admin.GetFromJsonAsync<AdminDashboardResponse>("/api/admin/dashboard"))!;

        Assert.Equal(16, dashboard.Summary.ActiveTasks);
        Assert.Equal(15, dashboard.Attention.Count);
        Assert.Equal(15, dashboard.Attention.Select(item => item.TaskId).Distinct().Count());
        Assert.All(dashboard.Attention, item => Assert.Single(item.Assignees, assignee => assignee.Id == adminUser.Id));
    }

    private static async Task<Setup> CreateSetupAsync(HttpClient client, Guid memberId)
    {
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Proyecto Dashboard" });
        projectResponse.EnsureSuccessStatusCode();
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;

        var membershipResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = memberId });
        if (membershipResponse.StatusCode != HttpStatusCode.Conflict)
        {
            membershipResponse.EnsureSuccessStatusCode();
        }

        var stageResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Entrega" });
        stageResponse.EnsureSuccessStatusCode();
        var stage = (await stageResponse.Content.ReadFromJsonAsync<StageResponse>())!;
        return new Setup(project, stage);
    }

    private static async Task<TaskResponse> CreateTaskAsync(HttpClient client, Setup setup, string title, string priority, string? dueDate, IReadOnlyList<Guid> assigneeIds)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{setup.Project.Id}/tasks", new
        {
            stageId = setup.Stage.Id,
            title,
            priority,
            dueDate,
            assigneeIds
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }

    private static async Task BlockAsync(HttpClient client, TaskResponse task)
    {
        var response = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/blockers", new
        {
            type = "TechnicalIssue",
            description = "Necesita intervención",
            version = task.Version
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task SubmitForReviewAsync(HttpClient client, Guid taskId)
    {
        var task = await GetTaskAsync(client, taskId);
        (await client.PostAsJsonAsync($"/api/tasks/{taskId}/start", new { version = task.Version })).EnsureSuccessStatusCode();
        task = await GetTaskAsync(client, taskId);
        (await client.PostAsJsonAsync($"/api/tasks/{taskId}/submit-review", new { version = task.Version })).EnsureSuccessStatusCode();
    }

    private static async Task CompleteAsync(HttpClient client, Guid taskId)
    {
        await SubmitForReviewAsync(client, taskId);
        var task = await GetTaskAsync(client, taskId);
        (await client.PostAsJsonAsync($"/api/tasks/{taskId}/complete", new { version = task.Version })).EnsureSuccessStatusCode();
    }

    private static async Task CancelAsync(HttpClient client, Guid taskId)
    {
        var task = await GetTaskAsync(client, taskId);
        (await client.PostAsJsonAsync($"/api/tasks/{taskId}/cancel", new { version = task.Version })).EnsureSuccessStatusCode();
    }

    private static async Task<TaskResponse> GetTaskAsync(HttpClient client, Guid taskId) =>
        (await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{taskId}"))!;

    private sealed record Setup(ProjectCreatedResponse Project, StageResponse Stage);
}
