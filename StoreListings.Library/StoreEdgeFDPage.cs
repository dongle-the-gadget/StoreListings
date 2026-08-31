using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

/// <summary>
/// Represents the parsed page data from the StoreEdgeFD API.
/// </summary>
public class StoreEdgeFDPage
{
    // -----------------------------------
    // Standard Product Data
    // -----------------------------------
    public required string ProductId { get; set; }
    public required string Title { get; set; }
    public required Image Logo { get; set; }
    public required List<Image> Screenshots { get; set; }
    public required string ShortDescription { get; set; }
    public required string Description { get; set; }
    public required string PublisherName { get; set; }
    public required double Rating { get; set; }
    public required long RatingCount { get; set; }
    public required InstallerType InstallerType { get; set; }
    public long? Size { get; set; }
    public string? PackageFamilyName { get; set; }
    public DateTime? LastUpdateDate { get; set; }
    public string? Version { get; set; }

    [SetsRequiredMembers]
    private StoreEdgeFDPage(
        string productId,
        string title,
        Image logo,
        List<Image> screenshots,
        string shortDescription,
        string description,
        string publisherName,
        double rating,
        long ratingCount,
        InstallerType installerType,
        long? size,
        string? packageFamilyName,
        DateTime? lastUpdateDate,
        string? version
    )
    {
        ProductId = productId;
        Title = title;
        Logo = logo;
        Screenshots = screenshots;
        ShortDescription = shortDescription;
        Description = description;
        PublisherName = publisherName;
        Rating = rating;
        RatingCount = ratingCount;
        Size = size;
        InstallerType = installerType;
        PackageFamilyName = packageFamilyName;
        LastUpdateDate = lastUpdateDate;
        Version = version;
    }

    public static async Task<Result<StoreEdgeFDPage>> GetProductAsync(
        string productId,
        StoreEdgeFDArch architecture,
        Market market,
        Lang language,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            string url =
                $"https://storeedge.microsoft.com/v9.0/pages/pdp?market={market}&locale={language}-{market}&deviceFamily=Windows.Desktop&architecture={architecture}&itemType=Apps&productId={productId}";

            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = string.Empty;
                try
                {
                    errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                catch { }
                return Result<StoreEdgeFDPage>.Failure(
                    new Exception($"API Error {response.StatusCode}: {errorContent}")
                );
            }

            using JsonDocument jsondoc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            // ---------------------------------------------------------
            // 1. Payload Selection Logic
            // ---------------------------------------------------------
            JsonElement? targetPayload = null;
            JsonElement rootArray = jsondoc.RootElement;

            if (rootArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in rootArray.EnumerateArray())
                {
                    if (item.TryGetProperty("Payload", out JsonElement payload))
                    {
                        string? pIdInPayload = payload.GetStringSafe("ProductId");
                        if (
                            !string.IsNullOrEmpty(pIdInPayload)
                            && pIdInPayload.Equals(productId, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            targetPayload = payload;
                            break;
                        }
                    }
                }

                if (targetPayload == null && rootArray.GetArrayLength() > 0)
                {
                    JsonElement lastItem = rootArray.EnumerateArray().Last();
                    if (lastItem.TryGetProperty("Payload", out JsonElement payload))
                    {
                        targetPayload = payload;
                    }
                }
            }

            if (targetPayload == null)
            {
                return Result<StoreEdgeFDPage>.Failure(
                    new Exception("No valid payload found in response.")
                );
            }

            JsonElement root = targetPayload.Value;

            // ---------------------------------------------------------
            // 2. Parse Standard Data
            // ---------------------------------------------------------
            string id = root.GetStringSafe("ProductId");
            string title = root.GetStringSafe("Title");
            string publisher = root.GetStringSafe("PublisherName");
            var (shortDescription, description) = Helpers.ProcessDescriptions(root);
            double rating = root.GetDoubleSafe("AverageRating");
            long ratingCount = root.GetLongSafe("RatingCount");
            long? size = root.TryGetProperty("ApproximateSizeInBytes", out var sizeEl)
                ? sizeEl.GetInt64()
                : null;

            DateTime? lastUpdated = null;
            if (
                root.TryGetProperty("PackageLastUpdateDateUtc", out JsonElement dateEl)
                && dateEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dateEl.GetString(), out DateTime parsedDate)
            )
            {
                lastUpdated = parsedDate;
            }

            string? pfn = null;
            if (
                root.TryGetProperty("PackageFamilyNames", out JsonElement pfnArray)
                && pfnArray.ValueKind == JsonValueKind.Array
                && pfnArray.GetArrayLength() > 0
            )
            {
                pfn = pfnArray[0].GetString();
            }

            var (logo, screenshots) = ExtractImages(root);

            // ---------------------------------------------------------
            // 3. Extract Version & Type
            // ---------------------------------------------------------
            InstallerType installerType = DetermineInstallerType(
                root.GetPropertySafe("Installer").GetStringSafe("Type")
            );

            // Try to find version at root first
            string? version = root.GetStringSafe("Version");

            // If empty, and type is unpackaged, dig into Architectures to find the best matching version
            if (
                string.IsNullOrEmpty(version)
                && installerType == InstallerType.Unpackaged
                && root.TryGetProperty("Installer", out JsonElement installerObj)
                && installerObj.TryGetProperty("Architectures", out JsonElement architecturesObj)
            )
            {
                // Determine priority list based on requested architecture (excluding neutral)
                string[] priorities = GetUnpackagedArchPriorities(architecture.ToString());

                foreach (var archKey in priorities)
                {
                    // Check if the JSON has an entry for this architecture (e.g. "x64", "arm64")
                    if (
                        architecturesObj.TryGetProperty(archKey, out JsonElement archData)
                        && archData.ValueKind == JsonValueKind.Object
                    )
                    {
                        string? candidateVer = archData.GetStringSafe("Version");
                        if (!string.IsNullOrEmpty(candidateVer))
                        {
                            version = candidateVer;
                            break; // Found the best match, stop looking
                        }
                    }
                }
            }

            return Result<StoreEdgeFDPage>.Success(
                new StoreEdgeFDPage(
                    id,
                    title,
                    logo,
                    screenshots,
                    shortDescription,
                    description,
                    publisher,
                    rating,
                    ratingCount,
                    installerType,
                    size,
                    pfn,
                    lastUpdated,
                    version
                )
            );
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDPage>.Failure(ex);
        }
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    private static string[] GetUnpackagedArchPriorities(string arch)
    {
        return arch.ToLowerInvariant() switch
        {
            "arm64" => new[] { "arm64", "x64", "x86" },
            "x64" => new[] { "x64", "x86" },
            "x86" => new[] { "x86" },

            _ => new[] { arch.ToLowerInvariant() },
        };
    }

    private static (Image Logo, List<Image> Screenshots) ExtractImages(JsonElement payload)
    {
        var logos = new List<Image>();
        var screenshots = new List<Image>();

        if (
            payload.TryGetProperty("Images", out JsonElement images)
            && images.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement img in images.EnumerateArray())
            {
                string? url = img.GetStringSafe("Url");
                if (string.IsNullOrEmpty(url))
                    continue;

                if (url.StartsWith("//"))
                    url = "https:" + url;

                string type = img.GetStringSafe("ImageType") ?? "";
                int h = img.GetIntSafe("Height");
                int w = img.GetIntSafe("Width");

                var imageObj = new Image(url, "Transparent", h, w);

                if (
                    type.Equals("logo", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("icon", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("BoxArt", StringComparison.OrdinalIgnoreCase)
                )
                {
                    logos.Add(imageObj);
                }
                else if (type.Equals("screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    screenshots.Add(imageObj);
                }
            }
        }

        Image finalLogo =
            logos.LastOrDefault(x => x.Height == 300 && x.Width == 300)
            ?? logos
                .Where(x => x.Height == x.Width)
                .OrderByDescending(x => x.Height)
                .FirstOrDefault()
            ?? logos.FirstOrDefault()
            ?? new Image(string.Empty, "Transparent", 0, 0);

        return (finalLogo, screenshots);
    }

    private static InstallerType DetermineInstallerType(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return InstallerType.Unknown;

        return type.ToLowerInvariant() switch
        {
            "wpm" => InstallerType.Unpackaged,
            "msix" => InstallerType.Packaged,
            "windowsupdate" => InstallerType.Packaged,
            _ => InstallerType.Unknown,
        };
    }
}
