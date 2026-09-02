using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Projects;

public static class ProjectsModuleExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization(Policies.AuthenticatedUser);

        Features.CreateProject.MapEndpoint(group);
        Features.ListProjects.MapEndpoint(group);
        Features.GetProject.MapEndpoint(group);
        Features.UpdateProject.MapEndpoint(group);
        Features.ChangeProjectStatus.MapEndpoints(group);
        Features.AddProjectMember.MapEndpoint(group);
        Features.ListProjectMembers.MapEndpoint(group);
        Features.RemoveProjectMember.MapEndpoint(group);
        Features.ListUserProjects.MapEndpoint(app);
        Features.StagesEndpoints.MapEndpoints(group);

        return app;
    }
}
