using Nakama.Api.Modules.Identity.Authentication;
using Microsoft.Extensions.Hosting;

namespace Nakama.Api.BuildingBlocks.Configuration;

public sealed record RuntimeSettings(
    string ConnectionString,
    JwtOptions Jwt,
    IReadOnlyList<string> AllowedCorsOrigins,
    string AttachmentStoragePath);

public static class RuntimeConfiguration
{
    public static RuntimeSettings Validate(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("NakamaDatabase");
        RequireValue(connectionString, "ConnectionStrings:NakamaDatabase");

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        RequireValue(jwt.SigningKey, "Authentication:Jwt:SigningKey");
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

        if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain absolute HTTP or HTTPS origins.");
        }

        var storagePath = configuration["Attachments:StoragePath"];
        RequireValue(storagePath, "Attachments:StoragePath");

        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(storagePath))
        {
            throw new InvalidOperationException("Attachments:StoragePath must be an absolute persistent path outside Development.");
        }

        return new RuntimeSettings(connectionString!, jwt, allowedOrigins, storagePath!);
    }

    private static void RequireValue(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{settingName} must be configured through environment variables or secure configuration.");
        }
    }
}
