using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace KungConnect.Client.Services;

/// <summary>
/// Delegating handler that injects the current Bearer token into every outgoing
/// HTTP request for the "KungConnect" named client.
/// Registered as Singleton so it captures the ROOT IServiceProvider (not a
/// short-lived scope), ensuring IAuthService always resolves to the same
/// singleton instance that SetTokens() was called on.
/// </summary>
public sealed class AuthHeaderHandler(IServiceProvider sp) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is null)
        {
            var auth = sp.GetService<IAuthService>();
            var token = auth?.AccessToken;

            // Debug trace — remove after confirming fix
            try
            {
                var log = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "kc-auth-handler.log");
                System.IO.File.AppendAllText(log,
                    $"[{System.DateTime.Now:HH:mm:ss}] {request.Method} {request.RequestUri} " +
                    $"auth={auth is not null} token={token?.Length ?? -1}\n");
            }
            catch { /* ignore logging failures */ }

            if (token is { Length: > 0 })
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, ct);
    }
}
