using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Activity.Features;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class AuthenticationApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Login_valid_credentials_returns_token_and_current_user()
    {
        await factory.ResetDatabaseAsync();
        var login = await factory.LoginAsync("admin@nakama.test", "AdminPassword1");
        using var client = await factory.CreateAuthenticatedClientAsync("admin@nakama.test", "AdminPassword1");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.NotNull(login.AccessToken);
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal("admin@nakama.test", login.User.Email);
        Assert.NotNull(me);
        Assert.Equal(login.User.Id, me.Id);
        Assert.Equal("Admin", me.Role);
    }

    [PostgresFact]
    public async Task Login_rejects_wrong_password_unknown_and_inactive_users()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var inactive = await factory.CreateUserAsync(admin, "inactive@nakama.test");
        await admin.PostAsync($"/api/users/{inactive.Id}/deactivate", null);
        using var anonymous = factory.CreateAnonymousClient();

        var wrongPassword = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "admin@nakama.test", password = "wrong" });
        var unknown = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "nobody@nakama.test", password = PostgresApiFactory.DefaultPassword });
        var inactiveLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = inactive.Email, password = PostgresApiFactory.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
    }

    [PostgresFact]
    public async Task Missing_token_returns_401_and_collaborator_is_forbidden_from_admin_operation()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        using var collaborator = await factory.CreateCollaboratorClientAsync(admin, "collaborator@nakama.test");
        using var anonymous = factory.CreateAnonymousClient();

        var unauthenticated = await anonymous.GetAsync("/api/auth/me");
        var forbidden = await collaborator.PostAsJsonAsync("/api/users", new { fullName = "Denied", email = "denied@nakama.test", role = "Collaborator", password = PostgresApiFactory.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [PostgresFact]
    public async Task Collaborator_member_can_run_workflow_while_outsider_is_forbidden_and_activity_uses_authenticated_actor()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var collaboratorUser = await factory.CreateUserAsync(admin, "member@nakama.test");
        var outsiderUser = await factory.CreateUserAsync(admin, "outsider@nakama.test");
        var project = await CreateProjectAsync(admin);
        await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = collaboratorUser.Id });
        var stage = await admin.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Build" });
        var stageResponse = (await stage.Content.ReadFromJsonAsync<StageResponse>())!;
        var taskResponse = await admin.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stageResponse.Id, title = "Authenticated task", priority = "Medium" });
        var task = (await taskResponse.Content.ReadFromJsonAsync<TaskResponse>())!;
        using var collaborator = await factory.CreateAuthenticatedClientAsync(collaboratorUser.Email, PostgresApiFactory.DefaultPassword);
        using var outsider = await factory.CreateAuthenticatedClientAsync(outsiderUser.Email, PostgresApiFactory.DefaultPassword);

        var allowed = await collaborator.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version });
        var updated = await admin.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}");
        var rejected = await outsider.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = updated!.Version });
        var feed = await admin.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{task.Id}/activity");

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Contains(feed!.Items, item => item.ActivityType == "TaskStarted" && item.Actor.Id == collaboratorUser.Id);
    }

    [PostgresFact]
    public async Task Admin_can_run_admin_workflow_for_a_project_without_membership()
    {
        await factory.ResetDatabaseAsync();
        using var creator = await factory.CreateAdminClientAsync();
        var anotherAdmin = await factory.CreateUserAsync(creator, "workflow-admin@nakama.test", UserRole.Admin);
        var project = await CreateProjectAsync(creator);
        var stage = (await (await creator.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Build" })).Content.ReadFromJsonAsync<StageResponse>())!;
        var task = (await (await creator.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stage.Id, title = "Admin workflow", priority = "Medium" })).Content.ReadFromJsonAsync<TaskResponse>())!;
        using var adminWithoutMembership = await factory.CreateAuthenticatedClientAsync(anotherAdmin.Email, PostgresApiFactory.DefaultPassword);

        var started = await adminWithoutMembership.PostAsJsonAsync($"/api/tasks/{task.Id}/start", new { version = task.Version });
        task = (await creator.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        var submitted = await adminWithoutMembership.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review", new { version = task.Version });
        task = (await creator.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"))!;
        var completed = await adminWithoutMembership.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { version = task.Version });

        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
    }

    [PostgresFact]
    public async Task Demoted_admin_token_loses_global_project_access_on_the_next_request()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var demotedAdmin = await factory.CreateUserAsync(admin, "demoted-admin@nakama.test", UserRole.Admin);
        var project = await CreateProjectAsync(admin);
        using var staleToken = await factory.CreateAuthenticatedClientAsync(demotedAdmin.Email, PostgresApiFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, (await staleToken.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/users/{demotedAdmin.Id}/role", new { role = "Collaborator" })).StatusCode);

        var detailAfterDemotion = await staleToken.GetAsync($"/api/projects/{project.Id}");
        var listAfterDemotion = await staleToken.GetFromJsonAsync<List<ProjectListItemResponse>>("/api/projects");
        var dashboardAfterDemotion = await staleToken.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, detailAfterDemotion.StatusCode);
        Assert.DoesNotContain(listAfterDemotion!, item => item.Id == project.Id);
        Assert.Equal(HttpStatusCode.Forbidden, dashboardAfterDemotion.StatusCode);
    }

    [PostgresFact]
    public async Task Login_returns_429_after_the_configured_limit_for_the_same_account_and_origin()
    {
        await factory.ResetDatabaseAsync();
        using var anonymous = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < PostgresApiFactory.TestLoginRateLimitPermitLimit; attempt++)
        {
            var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "rate-limit@nakama.test", password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "rate-limit@nakama.test", password = "wrong" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [PostgresFact]
    public async Task Authenticated_unsafe_requests_require_the_csrf_token()
    {
        await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Remove("X-Nakama-Csrf");

        var response = await admin.PostAsJsonAsync("/api/projects", new { name = "CSRF protection" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [PostgresFact]
    public async Task Request_cannot_impersonate_actor_and_deactivated_token_is_blocked()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var collaboratorUser = await factory.CreateUserAsync(admin, "actor@nakama.test");
        using var collaborator = await factory.CreateAuthenticatedClientAsync(collaboratorUser.Email, PostgresApiFactory.DefaultPassword);

        var impersonation = await admin.PostAsJsonAsync("/api/projects", new { name = "Spoof", createdByUserId = Guid.NewGuid() });
        await admin.PostAsync($"/api/users/{collaboratorUser.Id}/deactivate", null);
        var deactivatedToken = await collaborator.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.BadRequest, impersonation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivatedToken.StatusCode);
    }

    private static async Task<ProjectCreatedResponse> CreateProjectAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/projects", new { name = "Authentication project" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
    }
}
