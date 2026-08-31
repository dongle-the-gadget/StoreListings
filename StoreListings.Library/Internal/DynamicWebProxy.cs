using System.Net;

namespace StoreListings.Library.Internal;

internal sealed class DynamicWebProxy : IWebProxy
{
    private volatile IWebProxy? _innerProxy = HttpClient.DefaultProxy;

    public IWebProxy? InnerProxy
    {
        get => _innerProxy;
        set => _innerProxy = value;
    }

    public ICredentials? Credentials
    {
        get => _innerProxy?.Credentials;
        set
        {
            var proxy = _innerProxy;
            if (proxy is not null)
                proxy.Credentials = value;
        }
    }

    public Uri? GetProxy(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var proxy = _innerProxy;
        return proxy?.GetProxy(destination);
    }

    public bool IsBypassed(Uri host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var proxy = _innerProxy;
        return proxy is null || proxy.IsBypassed(host);
    }
}
