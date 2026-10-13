using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WeihnachtsfeierBit.AdminClient;

namespace WeihnachtsfeierBit.AdminClient.Endpoints;

public static class AdminAuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAdminAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/register", RegisterAsync);
        endpoints.MapPost("/auth/login", LoginAsync);
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        HttpRequest request,
        AdminApiClient apiClient,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();

        try
        {
            var registration = await apiClient.RegisterAdminAsync(username, password, cancellationToken);
            await SignInAsync(httpContext, registration.Session);
            return Results.Redirect(AdminPath(httpContext, "/"));
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(AdminPath(httpContext, "/register?error=registration"));
        }
    }

    private static async Task<IResult> LoginAsync(
        HttpRequest request,
        AdminApiClient apiClient,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        try
        {
            var session = await apiClient.LoginAsync(
                form["username"].ToString().Trim(),
                form["password"].ToString(),
                cancellationToken);
            await SignInAsync(httpContext, session);
            return Results.Redirect(AdminPath(httpContext, "/"));
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(AdminPath(httpContext, "/login?error=credentials"));
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        AdminApiClient apiClient,
        CancellationToken cancellationToken)
    {
        var accessToken = httpContext.User.FindFirstValue("api_access_token");
        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                await apiClient.LogoutAsync(accessToken, cancellationToken);
            }
            catch (HttpRequestException)
            {
            }
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect(AdminPath(httpContext, "/login"));
    }

    private static string AdminPath(HttpContext httpContext, string path) =>
        $"{httpContext.Request.PathBase}{path}";

    private static async Task SignInAsync(HttpContext httpContext, AdminSessionResponse session)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, session.DisplayName),
            new Claim("api_access_token", session.AccessToken),
            new Claim("api_session_id", session.SessionId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = session.ExpiresAtUtc
            });
    }
}