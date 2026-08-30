namespace Nakama.Api.BuildingBlocks.Persistence;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddNakamaHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks();
        var connectionString = configuration.GetConnectionString("NakamaDatabase");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            healthChecks.AddCheck("postgresql", new PostgreSqlHealthCheck(connectionString));
        }

        return services;
    }
}
