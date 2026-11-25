using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Nakama.Api.Modules.Identity.Authentication;

public interface ILoginRateLimiter
{
    bool TryAcquire(IPAddress? remoteIpAddress, string? email);
}

public sealed class LoginRateLimiter : ILoginRateLimiter, IDisposable
{
    private const long MaxTrackedPartitions = 10_000;
    private readonly MemoryCache partitions = new(new MemoryCacheOptions { SizeLimit = MaxTrackedPartitions, CompactionPercentage = 0 });
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
        var key = PartitionKey(remoteIpAddress, email);
        lock (partitionsGate)
        {
            if (!partitions.TryGetValue<AttemptWindow>(key, out var attempts))
            {
                attempts = new AttemptWindow();
                partitions.Set(key, attempts, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = window
                });
                // If the bounded cache is full, fail closed for untracked identities.
                if (!partitions.TryGetValue<AttemptWindow>(key, out attempts)) return false;
            }

            if (attempts is null || attempts.Count >= permitLimit) return false;
            attempts.Count++;
            return true;
        }
    }

    public void Dispose() => partitions.Dispose();

    private static string PartitionKey(IPAddress? remoteIpAddress, string? email)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var value = $"{remoteIpAddress?.ToString() ?? "unknown"}\n{normalizedEmail}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class AttemptWindow
    {
        public int Count { get; set; }
    }
}
