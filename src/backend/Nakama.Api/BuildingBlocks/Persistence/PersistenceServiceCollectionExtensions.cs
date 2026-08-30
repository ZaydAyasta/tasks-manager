using Microsoft.EntityFrameworkCore;

namespace Nakama.Api.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddNakamaPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NakamaDatabase");

        services.AddDbContext<NakamaDbContext>(options => options.UseNpgsql(
            string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException("ConnectionStrings:NakamaDatabase must be configured before resolving NakamaDbContext.")
                : connectionString));

        return services;
    }
}
