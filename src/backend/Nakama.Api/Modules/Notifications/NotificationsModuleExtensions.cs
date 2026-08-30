namespace Nakama.Api.Modules.Notifications;

public static class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) => services;
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app) => app;
}
