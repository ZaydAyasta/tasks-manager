using System.Net;
using System.Threading;
using Nakama.Api.Modules.Identity.Authentication;
using Xunit;

namespace Nakama.Api.Tests.Identity;

public sealed class LoginRateLimiterTests
{
    [Fact]
    public void Concurrent_first_attempts_do_not_exceed_the_partition_limit()
    {
        using var limiter = new LoginRateLimiter(permitLimit: 10, window: TimeSpan.FromMinutes(1));
        var acquiredPermits = 0;

        Parallel.For(0, 100, _ =>
        {
            if (limiter.TryAcquire(IPAddress.Loopback, "concurrent@test.local"))
            {
                Interlocked.Increment(ref acquiredPermits);
            }
        });

        Assert.Equal(10, acquiredPermits);
    }

    [Fact]
    public void At_capacity_the_limiter_keeps_existing_counts_and_rejects_untracked_requests()
    {
        using var limiter = new LoginRateLimiter(permitLimit: 2, window: TimeSpan.FromMinutes(1));

        for (var i = 0; i < 10_000; i++)
            Assert.True(limiter.TryAcquire(IPAddress.Loopback, $"fixture-{i}@nakama.test"));

        Assert.False(limiter.TryAcquire(IPAddress.Loopback, "untracked@nakama.test"));
        Assert.True(limiter.TryAcquire(IPAddress.Loopback, "fixture-0@nakama.test"));
        Assert.False(limiter.TryAcquire(IPAddress.Loopback, "fixture-0@nakama.test"));
    }
}
