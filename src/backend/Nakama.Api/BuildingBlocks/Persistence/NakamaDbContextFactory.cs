using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Nakama.Api.BuildingBlocks.Persistence;

public sealed class NakamaDbContextFactory : IDesignTimeDbContextFactory<NakamaDbContext>
{
    public NakamaDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<global::Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("NakamaDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:NakamaDatabase must be configured before running Entity Framework commands.");

        var options = new DbContextOptionsBuilder<NakamaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NakamaDbContext(options);
    }
}
