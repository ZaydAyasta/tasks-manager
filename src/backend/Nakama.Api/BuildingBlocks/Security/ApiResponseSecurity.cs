using Microsoft.Extensions.Hosting;

namespace Nakama.Api.BuildingBlocks.Security;

public static class ApiResponseSecurity
{
    public static Task ApplyAsync(HttpContext context, RequestDelegate next, IHostEnvironment environment)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(static state =>
            {
                var (response, isProduction) = ((HttpResponse Response, bool IsProduction))state;
                response.Headers.CacheControl = "no-store, max-age=0";
                response.Headers.Pragma = "no-cache";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["X-Frame-Options"] = "DENY";
                response.Headers["Referrer-Policy"] = "no-referrer";
                response.Headers["Content-Security-Policy"] = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";

                if (isProduction)
                {
                    response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            }, (context.Response, !environment.IsDevelopment()));
        }

        return next(context);
    }
}
