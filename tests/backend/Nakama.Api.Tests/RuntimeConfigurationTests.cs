using Microsoft.Extensions.Configuration;
using Nakama.Api.BuildingBlocks.Configuration;
using Xunit;

namespace Nakama.Api.Tests;

public sealed class RuntimeConfigurationTests
{
    [Fact]
    public void Production_rejects_a_missing_jwt_signing_key()
    {
        var configuration = CreateConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Authentication:Jwt:SigningKey", exception.Message);
    }

    [Fact]
    public void Production_rejects_a_missing_database_connection_or_cors_origin()
    {
        var missingDatabase = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NakamaDatabase"] = "",
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });
        var missingCors = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = Path.GetTempPath(),
            ["Cors:AllowedOrigins:0"] = ""
        });

        var databaseException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(missingDatabase, "Production"));
        var corsException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(missingCors, "Production"));

        Assert.Contains("ConnectionStrings:NakamaDatabase", databaseException.Message);
        Assert.Contains("Cors:AllowedOrigins", corsException.Message);
    }

    [Fact]
    public void Production_rejects_missing_jwt_issuer_or_audience()
    {
        var missingIssuer = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Authentication:Jwt:Issuer"] = "",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });
        var missingAudience = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Authentication:Jwt:Audience"] = "",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var issuerException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(missingIssuer, "Production"));
        var audienceException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(missingAudience, "Production"));

        Assert.Contains("Authentication:Jwt:Issuer", issuerException.Message);
        Assert.Contains("Authentication:Jwt:Audience", audienceException.Message);
    }

    [Fact]
    public void Production_rejects_a_non_positive_jwt_lifetime()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Authentication:Jwt:AccessTokenMinutes"] = "0",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Authentication:Jwt:AccessTokenMinutes", exception.Message);
    }

    [Fact]
    public void Production_rejects_a_cors_origin_outside_http_or_https()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Cors:AllowedOrigins:0"] = "ftp://nakama.internal",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Cors:AllowedOrigins", exception.Message);
    }

    [Fact]
    public void Production_rejects_a_missing_attachment_storage_path()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Attachments:StoragePath", exception.Message);
    }

    [Fact]
    public void Production_rejects_a_relative_attachment_path()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = "App_Data/attachments"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Attachments:StoragePath", exception.Message);
    }

    [Fact]
    public void Production_accepts_external_database_jwt_cors_and_persistent_storage_configuration()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var settings = RuntimeConfiguration.Validate(configuration, "Production");

        Assert.Equal("Host=db.internal;Database=nakama", settings.ConnectionString);
        Assert.Equal("Nakama.Api", settings.Jwt.Issuer);
        Assert.Equal("Nakama.Spa", settings.Jwt.Audience);
        Assert.Equal(new[] { "https://nakama.internal" }, settings.AllowedCorsOrigins);
        Assert.Equal(Path.GetTempPath(), settings.AttachmentStoragePath);
    }

    [Fact]
    public void Development_allows_the_local_relative_attachment_path()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = "App_Data/attachments"
        });

        var settings = RuntimeConfiguration.Validate(configuration, "Development");

        Assert.Equal("App_Data/attachments", settings.AttachmentStoragePath);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:NakamaDatabase"] = "Host=db.internal;Database=nakama",
            ["Authentication:Jwt:Issuer"] = "Nakama.Api",
            ["Authentication:Jwt:Audience"] = "Nakama.Spa",
            ["Authentication:Jwt:AccessTokenMinutes"] = "60",
            ["Cors:AllowedOrigins:0"] = "https://nakama.internal",
            ["Attachments:StoragePath"] = ""
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
