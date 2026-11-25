using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class PasswordManagementApiTests(PostgresApiFactory factory)
{
    private const string Temporary = "TemporaryPassword123";
    private const string Personal = "PersonalPassword456";

    [PostgresFact]
    public async Task Local_username_account_requires_change_before_work_and_accepts_personal_password()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var created = await admin.PostAsJsonAsync("/api/users", new { fullName = "Ana Equipo", email = "ana", role = "Collaborator", password = Temporary, mustChangePassword = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserDetailResponse>())!;
        Assert.Equal("ana@trello.local", user.Email);
        Assert.True(user.MustChangePassword);
        using var temporarySession = await factory.CreateAuthenticatedClientAsync(" ANA ", Temporary);
        using var staleSession = await factory.CreateAuthenticatedClientAsync("ana", Temporary);
        var me = await temporarySession.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(me!.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await temporarySession.GetAsync("/api/me/work")).StatusCode);
        var changed = await temporarySession.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Temporary, newPassword = Personal });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.False((await changed.Content.ReadFromJsonAsync<LoginResponse>())!.User.MustChangePassword);
        using var personalSession = await factory.CreateAuthenticatedClientAsync("ana", Personal);
        Assert.Equal(HttpStatusCode.OK, (await personalSession.GetAsync("/api/me/work")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staleSession.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await temporarySession.GetAsync("/api/auth/me")).StatusCode);
        using var anonymous = factory.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "ana", password = Temporary })).StatusCode);
    }

    [PostgresFact]
    public async Task Admin_reset_revokes_existing_session_and_requires_new_password()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync(admin, "recover@trello.local");
        using var oldSession = await factory.CreateAuthenticatedClientAsync(user.Email, PostgresApiFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new { temporaryPassword = Temporary })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/auth/me")).StatusCode);
        using var resetSession = await factory.CreateAuthenticatedClientAsync("recover", Temporary);
        Assert.True((await resetSession.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await resetSession.GetAsync("/api/projects")).StatusCode);
    }

    [PostgresFact]
    public async Task Wrong_current_short_same_and_oversized_passwords_leave_credentials_unchanged()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync(admin, "validation@trello.local", password: Personal);
        using var client = await factory.CreateAuthenticatedClientAsync(user.Email, Personal);
        foreach (var pair in new[] { ("wrong", Temporary), (Personal, "short"), (Personal, Personal), (Personal, new string('a', 129)) })
        {
            var response = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = pair.Item1, newPassword = pair.Item2 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var unchanged = await factory.CreateAuthenticatedClientAsync(user.Email, Personal);
        Assert.False((await unchanged.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.MustChangePassword);
    }

    [PostgresFact]
    public async Task Collaborator_and_anonymous_cannot_reset_someone_elses_password()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var target = await factory.CreateUserAsync(admin, "target@trello.local");
        using var member = await factory.CreateCollaboratorClientAsync(admin, "other@trello.local");
        using var anonymous = factory.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/users/{target.Id}/reset-password", new { temporaryPassword = Temporary })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/users/{target.Id}/reset-password", new { temporaryPassword = Temporary })).StatusCode);
    }

    [PostgresFact]
    public async Task Admin_reset_validates_target_and_password_without_changing_credentials()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var me = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        var target = await factory.CreateUserAsync(admin, "reset-validation@trello.local", password: Temporary);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/users/{me.Id}/reset-password", new { temporaryPassword = Personal })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/reset-password", new { temporaryPassword = Personal })).StatusCode);
        foreach (var password in new[] { "short", Temporary, new string('a', 129) })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/users/{target.Id}/reset-password", new { temporaryPassword = password })).StatusCode);
        using var unchanged = await factory.CreateAuthenticatedClientAsync(target.Email, Temporary);
        Assert.False((await unchanged.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.MustChangePassword);
    }

    [PostgresFact]
    public async Task Administrator_cannot_disable_or_demote_own_account()
    {
        await factory.ResetDatabaseAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var me = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/users/{me.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/users/{me.Id}/role", new { role = "Collaborator" })).StatusCode);
        var current = (await admin.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        Assert.True(current.IsActive);
        Assert.Equal("Admin", current.Role);
    }
}
