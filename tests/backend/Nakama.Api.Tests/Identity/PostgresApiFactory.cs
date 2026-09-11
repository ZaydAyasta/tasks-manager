using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nakama.Api.Modules.Identity.Features;

namespace Nakama.Api.Tests.Identity;

public class PostgresApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultPassword = "Password1";
    public const int TestLoginRateLimitPermitLimit = 100;
    private const string AdminEmail = "admin@nakama.test";
    private const string AdminPassword = "AdminPassword1";
    private const string JwtIssuer = "Nakama.Api.Tests";
    private const string JwtAudience = "Nakama.Api.Tests.Client";
    private const string JwtSigningKey = "test-signing-key-that-is-long-enough-and-not-used-outside-tests";
    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private readonly string? testConnectionString;
    private readonly string attachmentStoragePath = Path.Combine(Path.GetTempPath(), "nakama-api-tests", Guid.NewGuid().ToString("N"));
    private Guid adminId;

    protected virtual string EnvironmentName => "Development";

    public PostgresApiFactory() => testConnectionString = PostgresTestSettings.ConnectionString;

    public bool HasTestDatabase => !string.IsNullOrWhiteSpace(testConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Authentication:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Authentication:Jwt:Audience", JwtAudience);
        builder.UseSetting("Authentication:Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Authentication:Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("Authentication:LoginRateLimit:PermitLimit", TestLoginRateLimitPermitLimit.ToString());
        builder.UseSetting("ConnectionStrings:NakamaDatabase", testConnectionString ?? string.Empty);
        builder.UseSetting("Cors:AllowedOrigins:0", EnvironmentName == "Production" ? "https://nakama.test" : "http://localhost:5173");
        builder.UseSetting("AllowedHosts", "localhost");
        builder.UseSetting("Attachments:StoragePath", attachmentStoragePath);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NakamaDatabase"] = testConnectionString,
            ["Authentication:Jwt:Issuer"] = JwtIssuer,
            ["Authentication:Jwt:Audience"] = JwtAudience,
            ["Authentication:Jwt:SigningKey"] = JwtSigningKey,
            ["Authentication:Jwt:AccessTokenMinutes"] = "60",
            ["Authentication:LoginRateLimit:PermitLimit"] = TestLoginRateLimitPermitLimit.ToString(),
            ["Cors:AllowedOrigins:0"] = EnvironmentName == "Production" ? "https://nakama.test" : "http://localhost:5173",
            ["AllowedHosts"] = "localhost",
            ["DevelopmentBootstrap:Admin:Email"] = string.Empty,
            ["DevelopmentBootstrap:Admin:Password"] = string.Empty
            , ["Attachments:StoragePath"] = attachmentStoragePath
        }));
        builder.ConfigureServices(services =>
        {
            if (!string.IsNullOrWhiteSpace(testConnectionString))
            {
                services.RemoveAll<DbContextOptions<NakamaDbContext>>();
                services.RemoveAll<NakamaDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<NakamaDbContext>>();
                services.AddDbContext<NakamaDbContext>(options => options.UseNpgsql(testConnectionString));
            }
        });
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await ResetLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();

            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var seed = User.Create("Test Admin", "admin@nakama.test", UserRole.Admin, "seed", clock);
            var admin = User.Create(seed.FullName, seed.Email, seed.Role, hasher.HashPassword(seed, AdminPassword), clock);
            dbContext.Users.Add(admin);
            await dbContext.SaveChangesAsync(cancellationToken);
            adminId = admin.Id;
        }
        finally
        {
            ResetLock.Release();
        }
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions? options = null)
    {
        var client = base.CreateClient(options ?? new WebApplicationFactoryClientOptions());
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NakamaDbContext>();
        var admin = db.Users.Single(user => user.Id == adminId);
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().Create(admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.Add("X-Nakama-Csrf", token.CsrfToken);
        return client;
    }

    public HttpClient CreateAnonymousClient(WebApplicationFactoryClientOptions? options = null) =>
        base.CreateClient(options ?? new WebApplicationFactoryClientOptions());

    public async Task<UserDetailResponse> CreateUserAsync(HttpClient adminClient, string email, UserRole role = UserRole.Collaborator, string? password = null, string? fullName = null)
    {
        var response = await adminClient.PostAsJsonAsync("/api/users", new { fullName = fullName ?? email, email, role = role.ToString(), password = password ?? DefaultPassword });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDetailResponse>())!;
    }

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        using var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = CreateAnonymousClient();
        var login = await LoginAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken!);
        client.DefaultRequestHeaders.Add("X-Nakama-Csrf", login.CsrfToken);
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

    public async Task<HttpClient> CreateCollaboratorClientAsync(HttpClient adminClient, string email, string? password = null)
    {
        var actualPassword = password ?? DefaultPassword;
        await CreateUserAsync(adminClient, email, UserRole.Collaborator, actualPassword);
        return await CreateAuthenticatedClientAsync(email, actualPassword);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(attachmentStoragePath)) Directory.Delete(attachmentStoragePath, true);
    }
}

public sealed class ProductionPostgresApiFactory : PostgresApiFactory
{
    protected override string EnvironmentName => "Production";
}
