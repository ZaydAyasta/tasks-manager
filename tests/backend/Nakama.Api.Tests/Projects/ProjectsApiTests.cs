using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Projects;

[Collection(PostgresCollection.Name)]
public sealed class ProjectsApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Post_project_creates_owner_membership()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var creator = await CreateUserAsync(client, "owner@nakama.com");

        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Portal", description = "Redesign", startDate = "2026-09-01", endDate = "2026-10-15", createdByUserId = creator.Id });
        var project = await response.Content.ReadFromJsonAsync<ProjectCreatedResponse>();
        var members = await client.GetFromJsonAsync<List<ProjectMemberResponse>>($"/api/projects/{project!.Id}/members");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Active", project.Status);
        Assert.Single(members!);
        Assert.Equal("Owner", members![0].Role);
        Assert.Equal(creator.Id, members[0].UserId);
    }

    [PostgresFact]
    public async Task Post_project_rejects_missing_or_inactive_creator_and_invalid_dates()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var missing = await client.PostAsJsonAsync("/api/projects", new { name = "Portal", createdByUserId = Guid.NewGuid() });
        var creator = await CreateUserAsync(client, "inactive@nakama.com");
        var activeCreator = await CreateUserAsync(client, "active@nakama.com");
        await client.PostAsync($"/api/users/{creator.Id}/deactivate", null);
        var inactive = await client.PostAsJsonAsync("/api/projects", new { name = "Portal", createdByUserId = creator.Id });
        var invalidDates = await client.PostAsJsonAsync("/api/projects", new { name = "Portal", startDate = "2026-10-01", endDate = "2026-09-01", createdByUserId = activeCreator.Id });

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDates.StatusCode);
    }

    [PostgresFact]
    public async Task Members_enforce_membership_and_owner_rules()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var owner = await CreateUserAsync(client, "owner@nakama.com");
        var member = await CreateUserAsync(client, "member@nakama.com");
        var inactiveMember = await CreateUserAsync(client, "inactive-member@nakama.com");
        var project = await CreateProjectAsync(client, owner.Id);
        await client.PostAsync($"/api/users/{inactiveMember.Id}/deactivate", null);

        var added = await client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = member.Id });
        var duplicate = await client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = member.Id });
        var inactive = await client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId = inactiveMember.Id });
        var ownerRemoval = await client.DeleteAsync($"/api/projects/{project.Id}/members/{owner.Id}");
        var memberRemoval = await client.DeleteAsync($"/api/projects/{project.Id}/members/{member.Id}");

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ownerRemoval.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, memberRemoval.StatusCode);
    }

    [PostgresFact]
    public async Task Get_project_and_user_projects_return_related_data()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var owner = await CreateUserAsync(client, "owner@nakama.com");
        var outsider = await CreateUserAsync(client, "outsider@nakama.com");
        var project = await CreateProjectAsync(client, owner.Id);

        var detail = await client.GetFromJsonAsync<ProjectDetailResponse>($"/api/projects/{project.Id}");
        var ownerProjects = await client.GetFromJsonAsync<List<ProjectListItemResponse>>($"/api/users/{owner.Id}/projects");
        var outsiderProjects = await client.GetFromJsonAsync<List<ProjectListItemResponse>>($"/api/users/{outsider.Id}/projects");

        Assert.Equal(owner.Id, detail!.Owner.Id);
        Assert.Single(detail.Members);
        Assert.Single(ownerProjects!);
        Assert.Empty(outsiderProjects!);
    }

    [PostgresFact]
    public async Task Update_with_stale_version_returns_conflict()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var owner = await CreateUserAsync(client, "owner@nakama.com");
        var project = await CreateProjectAsync(client, owner.Id);

        var firstUpdate = await client.PutAsJsonAsync($"/api/projects/{project.Id}", new { name = "Portal v2", version = project.Version });
        var staleUpdate = await client.PutAsJsonAsync($"/api/projects/{project.Id}", new { name = "Portal v3", version = project.Version });

        Assert.Equal(HttpStatusCode.NoContent, firstUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    private static async Task<UserDetailResponse> CreateUserAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/users", new { fullName = email, email, role = "Collaborator" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!;
    }

    private static async Task<ProjectCreatedResponse> CreateProjectAsync(HttpClient client, Guid creatorId)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name = "Portal", createdByUserId = creatorId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;
    }
}
