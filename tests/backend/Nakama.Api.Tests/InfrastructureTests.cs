using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nakama.Api.BuildingBlocks.Time;
using Xunit;

namespace Nakama.Api.Tests;

public sealed class InfrastructureTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
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

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }
}
