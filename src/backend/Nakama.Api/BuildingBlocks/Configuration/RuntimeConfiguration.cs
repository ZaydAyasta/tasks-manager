using Nakama.Api.Modules.Identity.Authentication;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Nakama.Api.BuildingBlocks.Configuration;

public sealed record RuntimeSettings(
    string ConnectionString,
    JwtOptions Jwt,
    IReadOnlyList<string> AllowedCorsOrigins,
    string AttachmentStoragePath,
    int LoginRateLimitPermitLimit,
    TimeSpan LoginRateLimitWindow);

public static class RuntimeConfiguration
{
    public static RuntimeSettings Validate(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("NakamaDatabase");
        RequireValue(connectionString, "ConnectionStrings:NakamaDatabase");

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        RequireValue(jwt.SigningKey, "Authentication:Jwt:SigningKey");
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must contain at least 32 bytes.");
        }
        RequireValue(jwt.Issuer, "Authentication:Jwt:Issuer");
        RequireValue(jwt.Audience, "Authentication:Jwt:Audience");
        if (jwt.AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("Authentication:Jwt:AccessTokenMinutes must be greater than zero.");
        }

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin.");
        }

        if (allowedOrigins.Any(origin => !IsHttpOrigin(origin)))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain absolute HTTP or HTTPS origins.");
        }

        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must use HTTPS outside Development.");
        }

        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            var allowedHosts = configuration["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(allowedHosts)
                || allowedHosts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(host => host == "*"))
            {
                throw new InvalidOperationException("AllowedHosts must restrict Production to the API host names.");
            }
        }

        var storagePath = configuration["Attachments:StoragePath"];
        RequireValue(storagePath, "Attachments:StoragePath");

        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(storagePath))
        {
            throw new InvalidOperationException("Attachments:StoragePath must be an absolute persistent path outside Development.");
        }

        var loginRateLimitPermitLimit = configuration.GetValue<int?>("Authentication:LoginRateLimit:PermitLimit") ?? 10;
        var loginRateLimitWindowSeconds = configuration.GetValue<int?>("Authentication:LoginRateLimit:WindowSeconds") ?? 60;
        if (loginRateLimitPermitLimit is < 1 or > 100 || loginRateLimitWindowSeconds is < 1 or > 3600)
        {
            throw new InvalidOperationException("Authentication:LoginRateLimit must define PermitLimit between 1 and 100 and WindowSeconds between 1 and 3600.");
        }

        return new RuntimeSettings(connectionString!, jwt, allowedOrigins, storagePath!, loginRateLimitPermitLimit, TimeSpan.FromSeconds(loginRateLimitWindowSeconds));
    }

    private static bool IsHttpOrigin(string origin) => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static void RequireValue(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{settingName} must be configured through environment variables or secure configuration.");
        }
    }
}
