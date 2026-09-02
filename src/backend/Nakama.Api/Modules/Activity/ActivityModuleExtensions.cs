using Nakama.Api.Modules.Identity.Authentication;

namespace Nakama.Api.Modules.Activity;

public static class ActivityModuleExtensions
{
    public static IServiceCollection AddActivityModule(this IServiceCollection services)
    {
        services.AddScoped<IActivityRecorder, ActivityRecorder>();
        return services;
    }

    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        Features.ActivityEndpoints.MapEndpoints(app);
        return app;
    }
}
