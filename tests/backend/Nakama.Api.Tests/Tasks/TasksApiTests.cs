using System.Net;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Features;
using Nakama.Api.Tests.Identity;
using Xunit;
namespace Nakama.Api.Tests.Tasks;
[Collection(PostgresCollection.Name)]
public sealed class TasksApiTests(PostgresApiFactory factory)
{
 [PostgresFact] public async Task Creates_filters_assigns_and_completes_task(){await factory.ResetDatabaseAsync();using var c=factory.CreateClient();var owner=await User(c,"owner@nakama.com");var member=await User(c,"member@nakama.com");var p=await Project(c,owner.Id);await c.PostAsJsonAsync($"/api/projects/{p.Id}/members",new{userId=member.Id});var s=await Stage(c,p.Id);var create=await c.PostAsJsonAsync($"/api/projects/{p.Id}/tasks",new{stageId=s.Id,title="  Core task ",priority="High",createdByUserId=owner.Id,assigneeIds=new[]{member.Id}});var task=await create.Content.ReadFromJsonAsync<TaskResponse>();Assert.Equal(HttpStatusCode.Created,create.StatusCode);Assert.Equal("Pending",task!.Status);Assert.Single(task.Assignees);var filtered=await c.GetFromJsonAsync<List<TaskResponse>>($"/api/projects/{p.Id}/tasks?assigneeId={member.Id}&priority=High");Assert.Single(filtered!);var start=await c.PostAsJsonAsync($"/api/tasks/{task.Id}/start",new{version=task.Version});var detail=await c.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}");var review=await c.PostAsJsonAsync($"/api/tasks/{task.Id}/submit-review",new{version=detail!.Version});detail=await c.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}");var complete=await c.PostAsJsonAsync($"/api/tasks/{task.Id}/complete",new{version=detail!.Version});detail=await c.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}");Assert.Equal(HttpStatusCode.NoContent,start.StatusCode);Assert.Equal(HttpStatusCode.NoContent,review.StatusCode);Assert.Equal(HttpStatusCode.NoContent,complete.StatusCode);Assert.Equal("Completed",detail!.Status);Assert.NotNull(detail.CompletedAt);}
 static async Task<UserDetailResponse>User(HttpClient c,string e){var r=await c.PostAsJsonAsync("/api/users",new{fullName=e,email=e,role="Collaborator"});r.EnsureSuccessStatusCode();return (await r.Content.ReadFromJsonAsync<UserDetailResponse>())!;}static async Task<ProjectCreatedResponse>Project(HttpClient c,Guid id){var r=await c.PostAsJsonAsync("/api/projects",new{name="P",createdByUserId=id});r.EnsureSuccessStatusCode();return (await r.Content.ReadFromJsonAsync<ProjectCreatedResponse>())!;}static async Task<StageResponse>Stage(HttpClient c,Guid id){var r=await c.PostAsJsonAsync($"/api/projects/{id}/stages",new{name="S"});r.EnsureSuccessStatusCode();return (await r.Content.ReadFromJsonAsync<StageResponse>())!;}
}
