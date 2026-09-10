using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nakama.Api.Modules.Identity.Features;
using Xunit;

namespace Nakama.Api.Tests.Identity;

[Collection(ProductionPostgresCollection.Name)]
public sealed class ProductionAuthenticationApiTests(ProductionPostgresApiFactory factory)
{
    [PostgresFact]
    public async Task Production_logout_expires_the_browser_session_cookie()
    {
        await factory.ResetDatabaseAsync();
        using var session = factory.CreateAnonymousClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        var login = await session.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@nakama.test", password = "AdminPassword1" });
        var payload = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;

        var beforeLogout = await session.GetAsync("/api/auth/me");
        session.DefaultRequestHeaders.Add("X-Nakama-Csrf", payload.CsrfToken);
        var logout = await session.PostAsync("/api/auth/logout", null);
        var afterLogout = await session.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("nakama.access-token=", Assert.Single(logout.Headers.GetValues("Set-Cookie")));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [PostgresFact]
    public async Task Production_rejects_requests_for_unconfigured_hosts()
    {
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Host = "untrusted.nakama.test";

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Production_login_uses_a_secure_HttpOnly_cookie_without_exposing_the_access_token()
    {
        await factory.ResetDatabaseAsync();
        using var anonymous = factory.CreateAnonymousClient();

        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "admin@nakama.test", password = "AdminPassword1" });
        var payload = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Null(payload.AccessToken);
        Assert.NotEmpty(payload.CsrfToken);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store, max-age=0", login.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", login.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", login.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("max-age=31536000; includeSubDomains", login.Headers.GetValues("Strict-Transport-Security").Single());

        using var cookieSession = factory.CreateAnonymousClient();
        cookieSession.DefaultRequestHeaders.Add("Cookie", setCookie.Split(';', 2)[0]);
        var meResponse = await cookieSession.GetAsync("/api/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(me);
        Assert.Equal(payload.User.Id, me.Id);
        Assert.Equal(payload.CsrfToken, me.CsrfToken);
        Assert.Equal("no-store, max-age=0", meResponse.Headers.CacheControl!.ToString());

        var missingCsrf = await cookieSession.PostAsJsonAsync("/api/projects", new { name = "Cookie CSRF denied" });
        cookieSession.DefaultRequestHeaders.Add("X-Nakama-Csrf", payload.CsrfToken);
        var acceptedCsrf = await cookieSession.PostAsJsonAsync("/api/projects", new { name = "Cookie CSRF accepted" });

        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);
        Assert.Equal(HttpStatusCode.Created, acceptedCsrf.StatusCode);
    }
}
