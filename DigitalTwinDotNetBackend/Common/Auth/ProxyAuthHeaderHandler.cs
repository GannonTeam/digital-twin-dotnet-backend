using System.Net.Http.Headers;
using Common.Config;
using Microsoft.Extensions.Options;

namespace Common.Auth;

public sealed class ProxyAuthHeaderHandler : DelegatingHandler
{
    public readonly IOptions<ProxyOptions> _opts;
    
    public ProxyAuthHeaderHandler(IOptions<ProxyOptions> opts) =>  _opts = opts;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var key = _opts.Value.ApiKey;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return base.SendAsync(request, cancellationToken);
    }
}