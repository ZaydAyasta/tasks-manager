using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nakama.Api.BuildingBlocks.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for request {RequestPath} with trace {TraceId}",
            httpContext.Request.Path, httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://httpstatuses.com/500"
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
