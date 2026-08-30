using Microsoft.Extensions.Configuration;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgresTestSettings.ConnectionString))
        {
            Skip = "Configure ConnectionStrings:NakamaTestDatabase to run PostgreSQL integration tests.";
        }
    }
}

internal static class PostgresTestSettings
{
    public static string? ConnectionString { get; } = new ConfigurationBuilder()
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .Build()
        .GetConnectionString("NakamaTestDatabase");
}
