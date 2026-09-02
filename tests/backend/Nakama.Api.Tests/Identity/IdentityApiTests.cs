using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class IdentityApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Post_users_creates_user()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", new { fullName = "Sofía Paredes", email = "Sofia@Nakama.com", role = "Collaborator", password = "Password1" });
        var user = await response.Content.ReadFromJsonAsync<UserDetailResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(user);
        Assert.Equal("Sofía Paredes", user.FullName);
        Assert.Equal("sofia@nakama.com", user.Email);
        Assert.Equal("Collaborator", user.Role);
        Assert.True(user.IsActive);
        Assert.NotNull(response.Headers.Location);
    }

    [PostgresFact]
    public async Task Post_users_returns_conflict_for_duplicate_email()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var request = new { fullName = "Sofía Paredes", email = "sofia@nakama.com", role = "Collaborator", password = "Password1" };
        await client.PostAsJsonAsync("/api/users", request);

        var response = await client.PostAsJsonAsync("/api/users", new { fullName = "Otra Sofía", email = "SOFIA@NAKAMA.COM", role = "Admin", password = "Password1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task Post_users_returns_bad_request_for_invalid_request()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", new { fullName = "", email = "invalid", role = "Owner", password = "Password1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task Get_user_returns_existing_user()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var created = await CreateUserAsync(client);

        var response = await client.GetAsync($"/api/users/{created.Id}");
        var user = await response.Content.ReadFromJsonAsync<UserDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(user);
        Assert.Equal(created.Id, user.Id);
    }

    [PostgresFact]
    public async Task Get_user_returns_not_found_for_unknown_id()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task Get_users_returns_simple_list()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        await CreateUserAsync(client);

        var response = await client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<UserListItemResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(users);
        Assert.Contains(users!, user => user.Email == "sofia@nakama.com");
    }

    [PostgresFact]
    public async Task Deactivate_and_activate_update_user_state()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var created = await CreateUserAsync(client);

        var deactivation = await client.PostAsync($"/api/users/{created.Id}/deactivate", content: null);
        var inactiveUser = await client.GetFromJsonAsync<UserDetailResponse>($"/api/users/{created.Id}");
        var activation = await client.PostAsync($"/api/users/{created.Id}/activate", content: null);
        var activeUser = await client.GetFromJsonAsync<UserDetailResponse>($"/api/users/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deactivation.StatusCode);
        Assert.False(inactiveUser!.IsActive);
        Assert.Equal(HttpStatusCode.NoContent, activation.StatusCode);
        Assert.True(activeUser!.IsActive);
    }

    [PostgresFact]
    public async Task Put_role_changes_user_role()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var created = await CreateUserAsync(client);

        var response = await client.PutAsJsonAsync($"/api/users/{created.Id}/role", new { role = "Admin" });
        var user = await client.GetFromJsonAsync<UserDetailResponse>($"/api/users/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Admin", user!.Role);
    }

    private async Task ResetDatabaseAsync()
    {
        await factory.ResetDatabaseAsync();
    }

    private static async Task<UserDetailResponse> CreateUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/users", new { fullName = "Sofía Paredes", email = "sofia@nakama.com", role = "Collaborator", password = "Password1" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!;
    }
}
