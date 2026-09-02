using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.User>, Microsoft.AspNetCore.Identity.PasswordHasher<Domain.User>>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.Admin);

        Features.CreateUser.MapEndpoint(group);
        Features.GetUser.MapEndpoint(group);
        Features.ListUsers.MapEndpoint(group);
        Features.ActivateUser.MapEndpoint(group);
        Features.DeactivateUser.MapEndpoint(group);
        Features.ChangeUserRole.MapEndpoint(group);

        Features.AuthEndpoints.MapEndpoints(app);
        return app;
    }
}
