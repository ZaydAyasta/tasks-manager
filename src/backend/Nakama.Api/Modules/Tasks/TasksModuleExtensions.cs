using Nakama.Api.Modules.Tasks.Infrastructure;

namespace Nakama.Api.Modules.Tasks;

public static class TasksModuleExtensions
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services, Infrastructure.AttachmentOptions attachmentOptions)
    {
        services.AddOptions<Infrastructure.AttachmentOptions>().BindConfiguration(Infrastructure.AttachmentOptions.SectionName);
        if (string.Equals(attachmentOptions.Provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddS3AttachmentStorage(attachmentOptions);
        }
        else
        {
            services.AddScoped<Infrastructure.IAttachmentStorage, Infrastructure.LocalAttachmentStorage>();
        }

        return services;
    }
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        Features.TasksEndpoints.MapEndpoints(app);
        Features.TaskBlockersEndpoints.MapEndpoints(app);
        Features.SubtasksEndpoints.MapEndpoints(app);
        Features.TaskDependenciesEndpoints.MapEndpoints(app);
        Features.WorkEndpoints.MapEndpoints(app);
        Features.TaskCommentsEndpoints.MapEndpoints(app);
        Features.TaskAttachmentsEndpoints.MapEndpoints(app);
        return app;
    }
}
