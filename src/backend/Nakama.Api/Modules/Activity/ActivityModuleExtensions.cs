namespace Nakama.Api.Modules.Activity;

public static class ActivityModuleExtensions
{
    public static IServiceCollection AddActivityModule(this IServiceCollection services) => services;
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app) => app;
}
