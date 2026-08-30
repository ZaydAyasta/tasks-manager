namespace Nakama.Api.Modules.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        Features.CreateUser.MapEndpoint(group);
        Features.GetUser.MapEndpoint(group);
        Features.ListUsers.MapEndpoint(group);
        Features.ActivateUser.MapEndpoint(group);
        Features.DeactivateUser.MapEndpoint(group);
        Features.ChangeUserRole.MapEndpoint(group);

        return app;
    }
}
