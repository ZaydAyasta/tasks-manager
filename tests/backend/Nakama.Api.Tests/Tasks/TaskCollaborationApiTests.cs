using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Nakama.Api.Modules.Activity.Features;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests.Tasks;

[Collection(PostgresCollection.Name)]
public sealed class TaskCollaborationApiTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Comments_trim_enforce_ownership_paginate_and_record_activity()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var author = await factory.CreateUserAsync(admin, "comment-author@nakama.test"); var other = await factory.CreateUserAsync(admin, "comment-other@nakama.test"); var setup = await Setup(admin, author.Id); using var authorClient = await factory.CreateAuthenticatedClientAsync(author.Email, PostgresApiFactory.DefaultPassword); using var otherClient = await factory.CreateAuthenticatedClientAsync(other.Email, PostgresApiFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateAnonymousClient().PostAsJsonAsync($"/api/tasks/{setup.Task.Id}/comments", new { content = "x" })).StatusCode);
        var createdResponse = await authorClient.PostAsJsonAsync($"/api/tasks/{setup.Task.Id}/comments", new { content = "  Nota útil  " }); Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode); var created = (await createdResponse.Content.ReadFromJsonAsync<TaskCommentResponse>())!; Assert.Equal("Nota útil", created.Content); Assert.Equal(author.Id, created.Author.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await authorClient.PostAsJsonAsync($"/api/tasks/{setup.Task.Id}/comments", new { content = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.PutAsJsonAsync($"/api/tasks/{setup.Task.Id}/comments/{created.Id}", new { content = "No" })).StatusCode);
        var edited = await authorClient.PutAsJsonAsync($"/api/tasks/{setup.Task.Id}/comments/{created.Id}", new { content = "Actualizada" }); Assert.Equal(HttpStatusCode.OK, edited.StatusCode); Assert.True((await edited.Content.ReadFromJsonAsync<TaskCommentResponse>())!.IsEdited);
        var feed = await authorClient.GetFromJsonAsync<TaskCommentFeedResponse>($"/api/tasks/{setup.Task.Id}/comments?limit=1"); Assert.Single(feed!.Items); Assert.Contains("CommentEdited", (await authorClient.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{setup.Task.Id}/activity"))!.Items.Select(x => x.ActivityType));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tasks/{setup.Task.Id}/comments/{created.Id}")).StatusCode); Assert.Contains("CommentDeleted", (await admin.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{setup.Task.Id}/activity"))!.Items.Select(x => x.ActivityType));
    }

    [PostgresFact]
    public async Task Attachments_validate_upload_download_and_delete_with_activity()
    {
        await factory.ResetDatabaseAsync(); using var admin = await factory.CreateAdminClientAsync(); var uploader = await factory.CreateUserAsync(admin, "attachment-owner@nakama.test"); var stranger = await factory.CreateUserAsync(admin, "attachment-stranger@nakama.test"); var setup = await Setup(admin, uploader.Id); using var client = await factory.CreateAuthenticatedClientAsync(uploader.Email, PostgresApiFactory.DefaultPassword); using var other = await factory.CreateAuthenticatedClientAsync(stranger.Email, PostgresApiFactory.DefaultPassword);
        using var bad = Form("bad.exe", "application/octet-stream", "x"); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/tasks/{setup.Task.Id}/attachments", bad)).StatusCode);
        using var upload = Form("../../nota.txt", "text/plain", "safe content"); var response = await client.PostAsync($"/api/tasks/{setup.Task.Id}/attachments", upload); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var attachment = (await response.Content.ReadFromJsonAsync<TaskAttachmentResponse>())!; Assert.Equal("nota.txt", attachment.FileName); Assert.Equal(uploader.Id, attachment.UploadedBy.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/tasks/{setup.Task.Id}/attachments/{attachment.Id}/download")).StatusCode); var download = await client.GetAsync($"/api/tasks/{setup.Task.Id}/attachments/{attachment.Id}/download"); Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal("safe content", await download.Content.ReadAsStringAsync()); Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/tasks/{setup.Task.Id}/attachments/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tasks/{setup.Task.Id}/attachments/{attachment.Id}")).StatusCode); Assert.Contains("AttachmentAdded", (await admin.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{setup.Task.Id}/activity"))!.Items.Select(x => x.ActivityType)); Assert.Contains("AttachmentDeleted", (await admin.GetFromJsonAsync<ActivityFeedResponse>($"/api/tasks/{setup.Task.Id}/activity"))!.Items.Select(x => x.ActivityType));
    }
    private static MultipartFormDataContent Form(string name, string type, string value) { var form = new MultipartFormDataContent(); var content = new ByteArrayContent(Encoding.UTF8.GetBytes(value)); content.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(content, "file", name); return form; }
    private static async Task<(ProjectCreatedResponse Project, StageResponse Stage, TaskResponse Task)> Setup(HttpClient admin, Guid userId) { var project = (await (await admin.PostAsJsonAsync("/api/projects", new { name = "Collaboration" })).Content.ReadFromJsonAsync<ProjectCreatedResponse>())!; await admin.PostAsJsonAsync($"/api/projects/{project.Id}/members", new { userId }); var stage = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/stages", new { name = "Stage" })).Content.ReadFromJsonAsync<StageResponse>())!; var task = (await (await admin.PostAsJsonAsync($"/api/projects/{project.Id}/tasks", new { stageId = stage.Id, title = "Task", priority = "Medium" })).Content.ReadFromJsonAsync<TaskResponse>())!; return (project, stage, task); }
}
