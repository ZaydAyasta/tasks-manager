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
}
