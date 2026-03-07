using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace KungConnect.Client.Services;

/// <summary>
/// Delegating handler that injects the current Bearer token into every outgoing
/// HTTP request for the "KungConnect" named client.
///
/// Uses <see cref="IServiceProvider"/> to resolve <see cref="IAuthService"/>
/// lazily (i.e. only when the first request is actually sent) to avoid the
/// circular construction dependency that would occur if we took
/// <see cref="IAuthService"/> directly in the constructor — AuthService itself
/// creates an HttpClient during its constructor, which would trigger handler
/// construction, which would require AuthService, which isn't built yet.
/// </summary>
public sealed class AuthHeaderHandler(IServiceProvider sp) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        // Only inject if the request doesn't already carry its own Authorization header.
        if (request.Headers.Authorization is null)
        {
            var auth = sp.GetService<IAuthService>();
            if (auth?.AccessToken is { Length: > 0 } token)
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, ct);
    }
}
