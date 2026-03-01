using KungConnect.Server.Services;

namespace KungConnect.Server.Middleware;

/// <summary>
/// Intercepts every request when server setup hasn't been completed yet.
///
/// Allowed through unconditionally:
///   /setup.html, /api/setup/*, /health, /openapi/*, /scalar/*, /favicon.ico
///
/// Everything else:
///   - API paths  → 503 JSON so clients can show a meaningful error
///   - All other  → 302 redirect to /setup.html
/// </summary>
public class SetupMiddleware(RequestDelegate next, ISetupService setupService)
{
    private static readonly string[] AllowedPrefixes =
    [
        "/setup.html",
        "/api/setup",
        "/health",
        "/openapi",
        "/scalar",
        "/favicon.ico",
        "/_framework",  // Blazor WASM bootstrap files — needed so /login can load
        "/css",
        "/js",
        "/lib",
        "/KungConnect.Client.Web.styles.css",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await setupService.IsSetupRequiredAsync())
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";

        if (AllowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Return a JSON 503 for API requests so clients get a meaningful error
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Server setup is required. Visit /setup.html to complete configuration."}""");
            return;
        }

        // Redirect everything else (browsers, SPA routes, etc.) to the setup wizard
        context.Response.Redirect("/setup.html");
    }
}
