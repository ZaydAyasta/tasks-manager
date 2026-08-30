namespace Nakama.Api.Modules.Reporting;

public static class ReportingModuleExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services) => services;
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app) => app;
}
