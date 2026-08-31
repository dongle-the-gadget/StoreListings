using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace StoreListings.Library.Internal;

public static class JsonExtensions
{
    public static string GetStringSafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.String
        )
        {
            var value = prop.GetString();
            if (value is not null)
            {
                return value;
            }
        }
        return string.Empty;
    }

    public static long GetLongSafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.Number
        )
        {
            return prop.GetInt64();
        }
        return 0;
    }

    public static int GetIntSafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.Number
        )
        {
            return prop.GetInt32();
        }
        return 0;
    }

    public static double GetDoubleSafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.Number
        )
        {
            return prop.GetDouble();
        }
        return 0;
    }

    public static bool GetBoolSafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
        )
        {
            if (prop.ValueKind == JsonValueKind.True)
                return true;
            if (prop.ValueKind == JsonValueKind.False)
                return false;
        }
        return false;
    }

    public static JsonElement GetPropertySafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
        )
        {
            return prop;
        }
        return default;
    }

    public static JsonElement? GetFirstArrayElementOrNull(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.Array
            && prop.GetArrayLength() > 0
        )
        {
            return prop[0];
        }
        return null;
    }

    public static JsonElement GetArraySafe(this JsonElement element, string propName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propName, out var prop)
            && prop.ValueKind == JsonValueKind.Array
        )
        {
            return prop;
        }
        return JsonDocument.Parse("[]").RootElement;
    }
}

internal static class Helpers
{
    private static readonly DynamicWebProxy _dynamicProxy = new();
    private static readonly HttpClient _storeHttpClient = CreateStoreHttpClient();
    private static readonly HttpClient _fe3HttpClient = CreateFE3HttpClient();

    public static IWebProxy? Proxy
    {
        get => _dynamicProxy.InnerProxy;
        set => _dynamicProxy.InnerProxy = value;
    }

    public static HttpClient GetStoreHttpClient() => _storeHttpClient;

    public static HttpClient GetFE3StoreHttpClient() => _fe3HttpClient;

    private static SocketsHttpHandler CreateHandler()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseProxy = true,
            Proxy = _dynamicProxy,
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            handler.SslOptions.RemoteCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;
        }

        return handler;
    }

    private static HttpClient CreateStoreHttpClient()
    {
        var client = new HttpClient(CreateHandler());
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("*/*")
        );
        client.DefaultRequestHeaders.Add("User-Agent", "WindowsStore/22512.1401.1101.0");
        client.DefaultRequestHeaders.Add("MS-CV", CorrelationVector.Increment());
        client.DefaultRequestHeaders.Add("OSIsGenuine", "True");
        client.DefaultRequestHeaders.Add("OSIsSMode", "False");
        return client;
    }

    private static HttpClient CreateFE3HttpClient()
    {
        var client = new HttpClient(CreateHandler());
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Windows-Update-Agent/10.0.10011.16384 Client-Protocol/2.1"
        );
        client.DefaultRequestHeaders.Connection.Add("keep-alive");
        return client;
    }

    public static string ToBase64Url(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    public static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var stringChars = new char[length];

        var randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(stringChars);
    }

    public static List<Card> GetCards(JsonElement cardsElement)
    {
        return cardsElement
            .EnumerateArray()
            .Select(card =>
            {
                // 1. Image Selection Logic
                var imagesElement = card.GetPropertySafe("Images");
                var images =
                    imagesElement.ValueKind == JsonValueKind.Array
                        ? imagesElement.EnumerateArray()
                        : Enumerable.Empty<JsonElement>();

                // Try to find the last 300x300 image, fallback to the first image available
                var targetImage = images
                    .LastOrDefault(img => img.GetIntSafe("Height") == 300 && img.GetIntSafe("Width") == 300);

                // If LastOrDefault returns default (Undefined), fallback to the first image
                if (targetImage.ValueKind == JsonValueKind.Undefined)
                {
                    targetImage = images.FirstOrDefault();
                }

                // 2. Background Color Logic
                string bgColor = targetImage.GetStringSafe("BackgroundColor");
                if (!bgColor.StartsWith('#'))
                {
                    bgColor = "Transparent";
                }

                // 3. Installer Type Logic
                string installerTypeStr = card.GetPropertySafe("Installer").GetStringSafe("Type");
                if (string.IsNullOrEmpty(installerTypeStr))
                {
                    installerTypeStr = card.GetStringSafe("InstallerType");
                }
                var installerType = installerTypeStr switch
                {
                    "WindowsUpdate" => InstallerType.Packaged,
                    "WPM" or "DirectInstall" => InstallerType.Unpackaged,
                    _ => InstallerType.Unknown,
                };

                // 4. Construct Card
                return new Card(
                    card.GetStringSafe("ProductId"),
                    card.GetStringSafe("Title"),
                    card.GetStringSafe("DisplayPrice"),
                    card.GetDoubleSafe("AverageRating"),
                    installerType,
                    new Image(
                        targetImage.GetStringSafe("Url"),
                        bgColor,
                        targetImage.GetIntSafe("Height"),
                        targetImage.GetIntSafe("Width")
                    )
                );
            })
            .ToList();
    }

    public static (string Short, string Full) ProcessDescriptions(JsonElement? payload)
    {
        string shortDesc = payload?.GetStringSafe("ShortDescription") ?? string.Empty;
        string fullDesc = string.Empty;

        string? desc1 = payload?.GetStringSafe("Description");
        string? desc2 = payload?.GetStringSafe("ProductDescription");

        if (!string.IsNullOrEmpty(desc1))
        {
            fullDesc = desc1;
        }
        else if (!string.IsNullOrEmpty(desc2))
        {
            fullDesc = desc2;
        }

        if (string.IsNullOrEmpty(shortDesc) && !string.IsNullOrEmpty(fullDesc))
        {
            int limitIndex = fullDesc.IndexOf("\r\n");

            if (limitIndex == -1)
                limitIndex = fullDesc.IndexOf('\n');

            if (limitIndex == -1)
            {
                int periodIndex = fullDesc.IndexOf('.');

                if (periodIndex != -1)
                    limitIndex = periodIndex + 1;
            }

            if (limitIndex != -1)
            {
                shortDesc = fullDesc[..limitIndex];
            }
        }

        return (shortDesc, fullDesc);
    }
}
