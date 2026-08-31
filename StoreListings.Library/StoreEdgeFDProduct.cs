using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

/// <summary>
/// Represents a product from the StoreEdgeFD API.
/// </summary>
public class StoreEdgeFDProduct
{
    /// <summary>
    /// The Store product ID.
    /// </summary>
    public required string ProductId { get; set; }

    /// <summary>
    /// The listing title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The short listing description, if available.
    /// </summary>
    public required string ShortDescription { get; set; }

    /// <summary>
    /// The full listing description, if available.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The publisher name.
    /// </summary>
    public required string PublisherName { get; set; }

    /// <summary>
    /// The list of screenshots.
    /// </summary>
    public required List<Image> Screenshots { get; set; }

    /// <summary>
    /// The logo image.
    /// </summary>
    public required Image Logo { get; set; }

    /// <summary>
    /// last updated date.
    /// </summary>
    public required string RevisionId { get; set; }

    /// <summary>
    /// The product rating.
    /// </summary>
    public required double Rating { get; set; }

    /// <summary>
    /// The number of ratings.
    /// </summary>
    public required long RatingCount { get; set; }

    /// <summary>
    /// The size of the product.
    /// </summary>
    public required long Size { get; set; }

    /// <summary>
    /// Indicates if the product is a bundle.
    /// </summary>
    public required bool IsBundle { get; set; }

    /// <summary>
    /// The installer type.
    /// </summary>
    public required InstallerType InstallerType { get; set; }

    public string? PackageFamilyName { get; set; }

    [SetsRequiredMembers]
    private StoreEdgeFDProduct(
        string productId,
        string title,
        Image logo,
        List<Image> screenshots,
        string shortDescription,
        string description,
        string publisherName,
        string revisionId,
        double rating,
        long ratingCount,
        long size,
        InstallerType installerType,
        bool isBundle,
        string? packageFamilyName
    )
    {
        ProductId = productId;
        Title = title;
        Logo = logo;
        Screenshots = screenshots;
        ShortDescription = shortDescription;
        Description = description;
        PublisherName = publisherName;
        RevisionId = revisionId;
        Rating = rating;
        RatingCount = ratingCount;
        Size = size;
        InstallerType = installerType;
        IsBundle = isBundle;
        PackageFamilyName = packageFamilyName;
    }

    public static async Task<Result<StoreEdgeFDProduct>> GetProductAsync(
        string productId,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            string url =
                $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{productId}?market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}&architecture=x64&deviceFamilyVersion=281475124959641";

            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    return Result<StoreEdgeFDProduct>.Failure(
                        new Exception($"API Error {response.StatusCode}: {errorContent}")
                    );
                }
                catch
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            using JsonDocument jsondoc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            JsonElement payload = jsondoc.RootElement.GetPropertySafe("Payload");

            string id = payload.GetStringSafe("ProductId") ?? productId;
            string title = payload.GetStringSafe("Title");
            string publisher = payload.GetStringSafe("PublisherName");
            string revisionId = payload.GetStringSafe("RevisionId");
            double rating = payload.GetDoubleSafe("AverageRating");
            long ratingCount = payload.GetLongSafe("RatingCount");
            long size = payload.GetLongSafe("ApproximateSizeInBytes");
            var (logo, screenshots) = ExtractImages(payload);
            var (shortDesc, fullDesc) = Helpers.ProcessDescriptions(payload);
            InstallerType installerType = DetermineInstallerType(
                payload.GetPropertySafe("Installer").GetStringSafe("Type")
            );
            JsonElement? firstSku = payload.GetFirstArrayElementOrNull("Skus");
            bool isBundle =
                firstSku?.GetPropertySafe("BundledSkus").ValueKind == JsonValueKind.Array;

            string? version = ExtractVersion(payload);

            string? pfn = payload.GetFirstArrayElementOrNull("PackageFamilyNames")?.GetString();

            return Result<StoreEdgeFDProduct>.Success(
                new(
                    id,
                    title,
                    logo,
                    screenshots,
                    shortDesc,
                    fullDesc,
                    publisher,
                    revisionId,
                    rating,
                    ratingCount,
                    size,
                    installerType,
                    isBundle,
                    pfn
                )
            );
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDProduct>.Failure(ex);
        }
    }

    // --------------

    // HELPERS

    // --------------

    private static (Image Logo, List<Image> Screenshots) ExtractImages(JsonElement payload)
    {
        var logos = new List<Image>();
        var tileImages = new List<Image>();
        var screenshots = new List<Image>();

        if (payload.TryGetProperty("Images", out JsonElement images))
        {
            foreach (JsonElement img in images.EnumerateArray())
            {
                string? url = img.GetStringSafe("Url");

                if (string.IsNullOrEmpty(url))
                    continue;

                string type = img.GetStringSafe("ImageType") ?? "";

                string bg = img.GetStringSafe("BackgroundColor") ?? "Transparent";

                if (!bg.StartsWith('#'))
                    bg = "Transparent";

                int h = img.GetIntSafe("Height");

                int w = img.GetIntSafe("Width");

                var imageObj = new Image(url, bg, h, w);

                if (
                    type.Equals("logo", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("Poster", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("BoxArt", StringComparison.OrdinalIgnoreCase)
                )
                {
                    logos.Add(imageObj);
                }
                else if (type.Equals("screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    screenshots.Add(imageObj);
                }
                else
                {
                    tileImages.Add(imageObj);
                }
            }
        }

        Image finalLogo =
            logos.LastOrDefault(img => img.Height == 100 && img.Width == 100)
            ?? logos.FirstOrDefault()
            ?? tileImages.FirstOrDefault(img => img.Height == img.Width)
            ?? tileImages.FirstOrDefault()
            ?? new Image(string.Empty, "Transparent", 0, 0);

        return (finalLogo, screenshots);
    }

    public static async Task<Result<List<StoreEdgeFDProduct>>> GetProductsByIdTypeAsync(
        List<string> productIds,
        StoreIdType idType,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            string url =
                $"https://storeedge.microsoft.com/v9.0/products?market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}";

            using var stream = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("IdType", idType.ToString());
                writer.WriteStartArray("ProductIds");
                foreach (var id in productIds)
                    writer.WriteStringValue(id);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            string jsonBody = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            using StringContent content = new StringContent(
                jsonBody,
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await client.PostAsync(
                url,
                content,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    return Result<List<StoreEdgeFDProduct>>.Failure(
                        new Exception($"API Error {response.StatusCode}: {errorContent}")
                    );
                }
                catch
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            using JsonDocument jsondoc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            JsonElement payload = jsondoc.RootElement.GetPropertySafe("Payload");

            var productsList = new List<StoreEdgeFDProduct>();

            // Iterate through the array.
            // This naturally preserves the order returned by the API.
            foreach (JsonElement productData in payload.GetArraySafe("Products").EnumerateArray())
            {
                string id = productData.GetStringSafe("ProductId");
                string title = productData.GetStringSafe("Title");
                string publisher = productData.GetStringSafe("PublisherName");
                var (shortDesc, fullDesc) = Helpers.ProcessDescriptions(productData);
                string revisionId = payload.GetStringSafe("RevisionId");

                double rating = productData.GetDoubleSafe("AverageRating");
                long ratingCount = productData.GetLongSafe("RatingCount");
                long size = productData.GetLongSafe("ApproximateSizeInBytes");

                var (logo, screenshots) = ExtractImages(productData);

                InstallerType installerType = DetermineInstallerType(
                    productData.GetStringSafe("InstallerType")
                );

                bool isBundle = false;
                if (productData.TryGetProperty("BundleIds", out JsonElement bundlesProperty))
                {
                    isBundle =
                        bundlesProperty.ValueKind == JsonValueKind.Array
                        && bundlesProperty.GetArrayLength() > 0;
                }

                string? pfn = productData
                    .GetFirstArrayElementOrNull("PackageFamilyNames")
                    ?.GetString();

                productsList.Add(
                    new StoreEdgeFDProduct(
                        id,
                        title,
                        logo,
                        screenshots,
                        shortDesc,
                        fullDesc,
                        publisher,
                        revisionId,
                        rating,
                        ratingCount,
                        size,
                        installerType,
                        isBundle,
                        pfn
                    )
                );
            }

            return Result<List<StoreEdgeFDProduct>>.Success(productsList);
        }
        catch (Exception ex)
        {
            return Result<List<StoreEdgeFDProduct>>.Failure(ex);
        }
    }

    private static InstallerType DetermineInstallerType(string? type) =>
        type switch
        {
            "WindowsUpdate" => InstallerType.Packaged,

            "WPM" or "DirectInstall" => InstallerType.Unpackaged,

            _ => InstallerType.Unknown,
        };

    private static string? ExtractVersion(JsonElement payload)
    {
        var architectures = payload.GetPropertySafe("Installer").GetPropertySafe("Architectures");

        if (architectures.ValueKind != JsonValueKind.Object)
            return null;

        return architectures.GetPropertySafe("x64").GetStringSafe("Version") ?? architectures
                .GetPropertySafe("x86")
                .GetStringSafe("Version");
    }

    public static async Task<
        Result<
            List<(
                string InstallerUrl,
                string FileName,
                string InstallerSwitches,
                string Version,
                string InstallerSha256,
                string architecture,
                string locale
            )>
        >
    > GetUnpackagedInstall(
        string productId,
        Market market,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            HttpClient client = Helpers.GetStoreHttpClient();

            string url = $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/packageManifests/{productId}?market={market}";

            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            JsonElement data = json.RootElement.GetPropertySafe("Data");

            var allInstallers =
                new List<(string, string, string, string, string, string, string)>();

            foreach (JsonElement versionObj in data.GetArraySafe("Versions").EnumerateArray())
            {
                string rawVersion = versionObj.GetStringSafe("PackageVersion");
                string version = ExtractNumericVersionPrefix(rawVersion);

                string packageName = versionObj.GetPropertySafe("DefaultLocale").GetStringSafe("PackageName");
                if (string.IsNullOrEmpty(packageName))
                {
                    packageName = "App";
                }

                foreach (
                    JsonElement installer in versionObj.GetArraySafe("Installers").EnumerateArray()
                )
                {
                    string installerUrl = installer.GetStringSafe("InstallerUrl");
                    string installerSha256 = installer.GetStringSafe("InstallerSha256");
                    string installerLocale = installer.GetStringSafe("InstallerLocale").ToLowerInvariant();

                    string installerSwitches = installer
                        .GetPropertySafe("InstallerSwitches")
                        .GetStringSafe("Silent");

                    string extension = installer.GetStringSafe("InstallerType").ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = "exe";
                    }

                    string architecture = installer.GetStringSafe("Architecture");
                    if (string.IsNullOrEmpty(architecture))
                    {
                        architecture = "neutral";
                    }

                    string fileName = string.IsNullOrEmpty(installerLocale)
                        ? $"{packageName}_{architecture}.{extension}"
                        : $"{packageName}_{installerLocale}_{architecture}.{extension}";

                    allInstallers.Add(
                        (
                            installerUrl,
                            fileName,
                            installerSwitches,
                            version,
                            installerSha256,
                            architecture,
                            installerLocale
                        )
                    );
                }
            }

            return Result<
                List<(
                    string InstallerUrl,
                    string FileName,
                    string InstallerSwitches,
                    string Version,
                    string InstallerSha256,
                    string architecture,
                    string locale
                )>
            >.Success(allInstallers);
        }
        catch (Exception ex)
        {
            return Result<
                List<(
                    string InstallerUrl,
                    string FileName,
                    string InstallerSwitches,
                    string Version,
                    string InstallerSha256,
                    string architecture,
                    string locale
                )>
            >.Failure(ex);
        }
    }

    private static string ExtractNumericVersionPrefix(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        ReadOnlySpan<char> span = s.AsSpan().Trim();
        int space = span.IndexOf(' ');
        return space > 0 ? span[..space].ToString() : span.ToString();
    }
}
