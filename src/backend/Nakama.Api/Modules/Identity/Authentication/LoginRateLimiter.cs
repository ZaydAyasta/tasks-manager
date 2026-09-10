using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace Nakama.Api.Modules.Identity.Authentication;

public interface ILoginRateLimiter
{
    bool TryAcquire(IPAddress? remoteIpAddress, string? email);
}

public sealed class LoginRateLimiter : ILoginRateLimiter, IDisposable
{
    private const long MaxTrackedPartitions = 10_000;
    private readonly MemoryCache partitions = new(new MemoryCacheOptions { SizeLimit = MaxTrackedPartitions });
    private readonly object partitionsGate = new();
    private readonly int permitLimit;
    private readonly TimeSpan window;

    public LoginRateLimiter(int permitLimit, TimeSpan window)
    {
        this.permitLimit = permitLimit;
        this.window = window;
    }

    public bool TryAcquire(IPAddress? remoteIpAddress, string? email)
    {
        FixedWindowRateLimiter limiter;
        lock (partitionsGate)
        {
            limiter = partitions.GetOrCreate(PartitionKey(remoteIpAddress, email), entry =>
            {
                entry.SetSize(1);
                entry.SetSlidingExpiration(window + window);
                entry.RegisterPostEvictionCallback(static (_, value, _, _) => ((FixedWindowRateLimiter)value!).Dispose());
                return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            })!;
        }

        using var lease = limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose() => partitions.Dispose();

    private static string PartitionKey(IPAddress? remoteIpAddress, string? email)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var value = $"{remoteIpAddress?.ToString() ?? "unknown"}\n{normalizedEmail}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
