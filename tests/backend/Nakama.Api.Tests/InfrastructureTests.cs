using Microsoft.Extensions.DependencyInjection;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Tests.Identity;
using Xunit;

namespace Nakama.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class InfrastructureTests(PostgresApiFactory factory)
{
    [PostgresFact]
    public void Service_provider_can_resolve_the_clock()
    {
        using var scope = factory.Services.CreateScope();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        Assert.NotNull(clock);
    }

    [Fact]
    public void System_clock_returns_a_utc_timestamp()
    {
        var timestamp = new SystemClock().UtcNow;

        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.True(timestamp <= DateTimeOffset.UtcNow);
    }

    [PostgresFact]
    public async Task Liveness_readiness_and_backward_compatible_health_endpoints_return_success()
    {
        using var client = factory.CreateAnonymousClient();

        var liveness = await client.GetAsync("/health/live");
        var readiness = await client.GetAsync("/health/ready");
        var health = await client.GetAsync("/health");

        Assert.True(liveness.IsSuccessStatusCode);
        Assert.True(readiness.IsSuccessStatusCode);
        Assert.True(health.IsSuccessStatusCode);
    }
}
