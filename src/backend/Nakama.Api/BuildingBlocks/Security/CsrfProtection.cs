using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Nakama.Api.BuildingBlocks.Security;

public static class CsrfProtection
{
    public const string ClaimType = "nakama.csrf";
    public const string HeaderName = "X-Nakama-Csrf";

    public static async Task ValidateAsync(HttpContext context, RequestDelegate next)
    {
        if (IsSafeMethod(context.Request.Method) || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var expected = context.User.FindFirst(ClaimType)?.Value;
        var submitted = context.Request.Headers[HeaderName].ToString();
        if (!TokensMatch(expected, submitted))
        {
            await Results.Problem(new ProblemDetails
            {
                Type = "https://nakama/errors/csrf-invalid",
                Title = "La solicitud no incluye una protección CSRF válida.",
                Status = StatusCodes.Status403Forbidden
            }).ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsSafeMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static bool TokensMatch(string? expected, string submitted)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(submitted))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var submittedBytes = Encoding.UTF8.GetBytes(submitted);
        return expectedBytes.Length == submittedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, submittedBytes);
    }
}
