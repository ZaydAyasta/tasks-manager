namespace Nakama.Api.Modules.Notifications;

public static class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) { services.AddScoped<INotificationWriter, NotificationWriter>(); return services; }
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app) { NotificationEndpoints.Map(app); return app; }
}
