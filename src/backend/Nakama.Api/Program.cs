using Nakama.Api.BuildingBlocks.Errors;
using Nakama.Api.BuildingBlocks.Configuration;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Security;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Dashboard;
using Nakama.Api.Modules.Identity;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Projects;
using Nakama.Api.Modules.Reporting;
using Nakama.Api.Modules.Tasks;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.IdentityModel.Tokens;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var hostingPort = builder.Configuration["PORT"];
if (int.TryParse(hostingPort, out var port) && port is > 0 and <= 65535)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
var runtimeSettings = RuntimeConfiguration.Validate(builder.Configuration, builder.Environment.EnvironmentName);
var allowedHosts = builder.Configuration["AllowedHosts"]!
    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddHostFiltering(options =>
{
    options.AllowedHosts.Clear();
    foreach (var host in allowedHosts)
    {
        options.AllowedHosts.Add(host);
    }
});

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddNakamaPersistence(builder.Configuration);
builder.Services.AddNakamaHealthChecks(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(runtimeSettings.AllowedCorsOrigins.ToArray())
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
var jwt = runtimeSettings.Jwt;
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = System.Security.Claims.ClaimTypes.Email,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var proof = context.Principal?.FindFirst(CsrfProtection.ClaimType)?.Value;
                var version = context.Principal?.FindFirst("security_version")?.Value ?? "0";
                if (!Guid.TryParse(subject, out var id) || string.IsNullOrEmpty(proof) || !int.TryParse(version, out var tokenVersion))
                {
                    context.Fail("Invalid session.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<NakamaDbContext>();
                var fingerprint = RevokedSession.FingerprintOf(proof);
                var valid = await db.Users.AnyAsync(user => user.Id == id && user.SecurityVersion == tokenVersion
                    && !db.RevokedSessions.Any(session => session.Fingerprint == fingerprint), context.HttpContext.RequestAborted);
                if (!valid) context.Fail("Session revoked.");
            },
            OnMessageReceived = context =>
            {
                var cookieToken = context.Request.Cookies["nakama.access-token"];
                if (!string.IsNullOrWhiteSpace(cookieToken)) context.Token = cookieToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser().AddRequirements(new ActiveUserRequirement(false)));
    options.AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(new ActiveUserRequirement(true)));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ILoginRateLimiter>(new LoginRateLimiter(runtimeSettings.LoginRateLimitPermitLimit, runtimeSettings.LoginRateLimitWindow));

builder.Services.AddIdentityModule();
builder.Services.AddProjectsModule();
builder.Services.AddTasksModule(runtimeSettings.Attachments);
builder.Services.AddNotificationsModule();
builder.Services.AddActivityModule();
builder.Services.AddDashboardModule();
builder.Services.AddReportingModule();

var app = builder.Build();

await Nakama.Api.Modules.Identity.Development.DevelopmentAdminSeeder.SeedAsync(app);
await Nakama.Api.Modules.Identity.Development.PilotDataSeeder.SeedAsync(app);

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, context) =>
    {
        diagnosticContext.Set("RequestPath", context.Request.Path.Value ?? "/");
        diagnosticContext.Set("TraceId", context.TraceIdentifier);
    };
});
app.UseExceptionHandler();
app.Use((context, next) => ApiResponseSecurity.ApplyAsync(context, next, app.Environment));
app.UseCors("Frontend");
app.UseAuthentication();
app.Use(CsrfProtection.ValidateAsync);
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health");
app.MapIdentityEndpoints();
app.MapProjectsEndpoints();
app.MapTasksEndpoints();
app.MapNotificationsEndpoints();
app.MapActivityEndpoints();
app.MapDashboardEndpoints();
app.MapReportingEndpoints();

app.Run();

public partial class Program;
