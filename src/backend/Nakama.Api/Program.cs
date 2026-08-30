using Nakama.Api.BuildingBlocks.Errors;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Identity;
using Nakama.Api.Modules.Notifications;
using Nakama.Api.Modules.Projects;
using Nakama.Api.Modules.Reporting;
using Nakama.Api.Modules.Tasks;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddIdentityModule();
builder.Services.AddProjectsModule();
builder.Services.AddTasksModule();
builder.Services.AddNotificationsModule();
builder.Services.AddActivityModule();
builder.Services.AddReportingModule();

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, context) =>
    {
        diagnosticContext.Set("RequestPath", context.Request.Path.Value ?? "/");
        diagnosticContext.Set("TraceId", context.TraceIdentifier);
    };
});
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapIdentityEndpoints();
app.MapProjectsEndpoints();
app.MapTasksEndpoints();
app.MapNotificationsEndpoints();
app.MapActivityEndpoints();
app.MapReportingEndpoints();

app.Run();

public partial class Program;
