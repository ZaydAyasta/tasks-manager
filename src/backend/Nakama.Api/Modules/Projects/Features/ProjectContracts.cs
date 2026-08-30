using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Features;

public sealed record CreateProjectRequest(string? Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, Guid? CreatedByUserId);
public sealed record UpdateProjectRequest(string? Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, int? Version);
public sealed record ProjectVersionRequest(int? Version);
public sealed record AddProjectMemberRequest(Guid? UserId);

public sealed record ProjectOwnerResponse(Guid Id, string FullName);
public sealed record ProjectMemberResponse(Guid UserId, string FullName, string Email, string Role, DateTimeOffset JoinedAt);
public sealed record ProjectCreatedResponse(Guid Id, string Name, string? Description, string Status, DateOnly? StartDate, DateOnly? EndDate, ProjectOwnerResponse Owner, DateTimeOffset CreatedAt, int Version);
public sealed record ProjectListItemResponse(Guid Id, string Name, string Status, DateOnly? StartDate, DateOnly? EndDate, int MemberCount);
public sealed record ProjectDetailResponse(Guid Id, string Name, string? Description, string Status, DateOnly? StartDate, DateOnly? EndDate, ProjectOwnerResponse Owner, IReadOnlyList<ProjectMemberResponse> Members, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);

internal static class ProjectMappings
{
    public static ProjectListItemResponse ToListItemResponse(this Project project, int memberCount) => new(
        project.Id, project.Name, project.Status.ToString(), project.StartDate, project.EndDate, memberCount);
}
