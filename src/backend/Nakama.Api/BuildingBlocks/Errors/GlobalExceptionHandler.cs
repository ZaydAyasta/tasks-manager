using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Nakama.Api.BuildingBlocks.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for request {RequestPath} with trace {TraceId}",
            httpContext.Request.Path, httpContext.TraceIdentifier);

        var status = exception is JsonException or BadHttpRequestException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status400BadRequest ? "The request body is invalid." : "An unexpected error occurred.",
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
