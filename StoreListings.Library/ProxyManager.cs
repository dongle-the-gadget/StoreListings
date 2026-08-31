using System.Net;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

/// <summary>
/// Configures proxy settings for StoreListings HTTP requests.
/// </summary>
public static class ProxyManager
{
    /// <summary>
    /// Gets or sets the proxy used for all Store and FE3 HTTP requests.
    /// Defaults to <see cref="HttpClient.DefaultProxy"/> (OS system proxy).
    /// Set to <see langword="null"/> for a direct connection.
    /// </summary>
    /// <remarks>
    /// The new proxy is resolved per request, so it takes effect on the next
    /// request after being set. Requests already in flight complete on the
    /// connection they started with.
    /// </remarks>
    public static IWebProxy? Proxy
    {
        get => Helpers.Proxy;
        set => Helpers.Proxy = value;
    }

    /// <summary>
    /// Sets the proxy using a proxy URI string (e.g., "http://127.0.0.1:8080").
    /// </summary>
    /// <param name="proxyUri">The proxy URL string.</param>
    /// <param name="credentials">Optional credentials for proxy authentication.</param>
    public static void SetProxy(string proxyUri, ICredentials? credentials = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proxyUri);
        Proxy = new WebProxy(proxyUri) { Credentials = credentials };
    }

    /// <summary>
    /// Sets the proxy using a <see cref="Uri"/> object.
    /// </summary>
    /// <param name="proxyUri">The proxy <see cref="Uri"/>.</param>
    /// <param name="credentials">Optional credentials for proxy authentication.</param>
    public static void SetProxy(Uri proxyUri, ICredentials? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(proxyUri);
        Proxy = new WebProxy(proxyUri) { Credentials = credentials };
    }

    /// <summary>
    /// Configures the library to use the operating system default / environment proxy.
    /// </summary>
    public static void UseSystemProxy() => Proxy = HttpClient.DefaultProxy;

    /// <summary>
    /// Disables any proxy and routes requests directly.
    /// </summary>
    public static void UseDirect() => Proxy = null;

    /// <summary>
    /// Gets whether requests are currently routed directly without a proxy.
    /// </summary>
    public static bool IsDirect => Proxy is null;

    /// <summary>
    /// Gets whether a proxy (system or custom) is currently configured.
    /// </summary>
    public static bool HasProxy => Proxy is not null;
}
