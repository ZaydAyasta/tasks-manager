namespace Nakama.Api.Modules.Tasks;

public static class TasksModuleExtensions
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services) => services;
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        Features.TasksEndpoints.MapEndpoints(app);
        Features.TaskBlockersEndpoints.MapEndpoints(app);
        Features.SubtasksEndpoints.MapEndpoints(app);
        return app;
    }
}
