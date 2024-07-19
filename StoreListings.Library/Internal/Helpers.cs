using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace StoreListings.Library.Internal;

internal static class Helpers
{
    private static HttpClientHandler? _handler;
    private static HttpClient? _storeHttpClient;
    private static HttpClient? _fe3HttpClient;

    public static HttpClient GetStoreHttpClient()
    {
        if (_storeHttpClient is not null)
            return _storeHttpClient;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _handler = new HttpClientHandler();
            _handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _storeHttpClient = new HttpClient(_handler);
        }
        else
        {
            _storeHttpClient = new HttpClient();
        }

        _storeHttpClient.DefaultRequestHeaders.Accept.Clear();
        _storeHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        _storeHttpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        _storeHttpClient.DefaultRequestHeaders.Add("User-Agent", "WindowsStore/22106.1401.2.0");

        _storeHttpClient.DefaultRequestHeaders.Add("MS-CV", CorrelationVector.Increment());
        _storeHttpClient.DefaultRequestHeaders.Add("OSIsGenuine", "True");
        _storeHttpClient.DefaultRequestHeaders.Add("OSIsSMode", "False");
        return _storeHttpClient;
    }

    public static HttpClient GetFE3StoreHttpClient()
    {
        if (_fe3HttpClient is not null)
            return _fe3HttpClient;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _handler = new HttpClientHandler();
            _handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _fe3HttpClient = new HttpClient(_handler);
        }
        else
        {
            _fe3HttpClient = new HttpClient();
        }

        _fe3HttpClient.DefaultRequestHeaders.Add("User-Agent", "Windows-Update-Agent/10.0.10011.16384 Client-Protocol/2.1");
        _fe3HttpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
        return _fe3HttpClient;
    }
}
