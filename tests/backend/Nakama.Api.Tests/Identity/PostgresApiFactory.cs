using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nakama.Api.BuildingBlocks.Persistence;

namespace Nakama.Api.Tests.Identity;

public sealed class PostgresApiFactory : WebApplicationFactory<Program>
{
    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private readonly string? testConnectionString;

    public PostgresApiFactory() => testConnectionString = PostgresTestSettings.ConnectionString;

    public bool HasTestDatabase => !string.IsNullOrWhiteSpace(testConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            if (!string.IsNullOrWhiteSpace(testConnectionString))
            {
                services.RemoveAll<DbContextOptions<NakamaDbContext>>();
                services.RemoveAll<NakamaDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<NakamaDbContext>>();
                services.AddDbContext<NakamaDbContext>(options => options.UseNpgsql(testConnectionString));
            }
        });
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await ResetLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();

            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            ResetLock.Release();
        }
    }
}
