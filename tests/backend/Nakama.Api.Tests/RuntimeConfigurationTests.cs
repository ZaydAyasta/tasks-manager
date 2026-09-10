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
    public void Production_rejects_a_jwt_signing_key_shorter_than_32_bytes()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "too-short",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

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
    public void Production_rejects_an_http_cors_origin()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Cors:AllowedOrigins:0"] = "http://nakama.internal",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Cors:AllowedOrigins", exception.Message);
    }

    [Fact]
    public void Production_rejects_missing_or_wildcard_allowed_hosts()
    {
        var missingHosts = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = Path.GetTempPath(),
            ["AllowedHosts"] = ""
        });
        var wildcardHosts = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:StoragePath"] = Path.GetTempPath(),
            ["AllowedHosts"] = "*"
        });

        var missingException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(missingHosts, "Production"));
        var wildcardException = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(wildcardHosts, "Production"));

        Assert.Contains("AllowedHosts", missingException.Message);
        Assert.Contains("AllowedHosts", wildcardException.Message);
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
        Assert.Equal("Local", settings.Attachments.Provider);
        Assert.Equal(Path.GetTempPath(), settings.Attachments.StoragePath);
        Assert.Equal(10, settings.LoginRateLimitPermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.LoginRateLimitWindow);
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

        Assert.Equal("App_Data/attachments", settings.Attachments.StoragePath);
    }

    [Fact]
    public void Development_allows_an_http_localhost_cors_origin()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            ["Attachments:StoragePath"] = "App_Data/attachments"
        });

        var settings = RuntimeConfiguration.Validate(configuration, "Development");

        Assert.Equal(new[] { "http://localhost:5173" }, settings.AllowedCorsOrigins);
    }

    [Fact]
    public void Production_rejects_an_invalid_login_rate_limit_configuration()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Authentication:LoginRateLimit:PermitLimit"] = "0",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Authentication:LoginRateLimit", exception.Message);
    }

    [Fact]
    public void Production_accepts_s3_attachments_without_a_local_storage_path()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:Provider"] = "S3",
            ["Attachments:StoragePath"] = "",
            ["Attachments:S3:ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
            ["Attachments:S3:BucketName"] = "nakama-attachments",
            ["Attachments:S3:AccessKeyId"] = "access-key",
            ["Attachments:S3:SecretAccessKey"] = "secret-key",
            ["Attachments:S3:Region"] = "auto"
        });

        var settings = RuntimeConfiguration.Validate(configuration, "Production");

        Assert.Equal("S3", settings.Attachments.Provider);
        Assert.Equal("nakama-attachments", settings.Attachments.S3.BucketName);
    }

    [Fact]
    public void Production_rejects_incomplete_s3_attachment_configuration()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:Provider"] = "S3",
            ["Attachments:StoragePath"] = "",
            ["Attachments:S3:ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
            ["Attachments:S3:BucketName"] = "",
            ["Attachments:S3:AccessKeyId"] = "access-key",
            ["Attachments:S3:SecretAccessKey"] = "secret-key",
            ["Attachments:S3:Region"] = "auto"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Attachments:S3:BucketName", exception.Message);
    }

    [Fact]
    public void Production_rejects_an_unknown_attachment_provider()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-for-validation",
            ["Attachments:Provider"] = "Filesystem",
            ["Attachments:StoragePath"] = Path.GetTempPath()
        });

        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(configuration, "Production"));

        Assert.Contains("Attachments:Provider", exception.Message);
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
            ["AllowedHosts"] = "api.nakama.internal",
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
