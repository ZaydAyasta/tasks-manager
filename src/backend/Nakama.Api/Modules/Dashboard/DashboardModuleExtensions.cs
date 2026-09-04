namespace Nakama.Api.Modules.Dashboard;

public static class DashboardModuleExtensions
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        Features.DashboardEndpoints.MapEndpoints(app);
        return app;
    }
}
