using System.Text.Json;
using StoreListings.Library.Internal;

namespace StoreListings.Library
{
    public class DCATPackage
    {
        public class FrameworkDependency
        {
            public required string PackageIdentity { get; set; }
            public required Version MinVersion { get; set; }
        }

        public class PlatformDependency
        {
            public required DeviceFamily Platform { get; set; }
            public required Version MinVersion { get; set; }
        }

        public class DCATProduct
        {
            public required string ProductId { get; set; }
            public required IEnumerable<DCATPackage> Packages { get; set; }
        }

        public required string ProductId { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string ShortDescription { get; set; }
        public required string PublisherName { get; set; }
        public required string RevisionId { get; set; }
        public required string PackageFamilyName { get; set; }
        public required string PackageIdentityName { get; set; }
        public required bool IsBundle { get; set; }
        public required double Rating { get; set; }
        public required long RatingCount { get; set; }
        public required Image Logo { get; set; }
        public required List<Image> Screenshots { get; set; }
        public string? PackageFullName { get; set; }
        public string? WuCategoryId { get; set; }
        public Version? AppVersion { get; set; }
        public long? Size { get; set; }
        public IEnumerable<FrameworkDependency>? FrameworkDependencies { get; set; }
        public IEnumerable<PlatformDependency>? PlatformDependencies { get; set; }

        /// <summary>
        /// Main Entry Point
        /// </summary>
        public static async Task<Result<IEnumerable<DCATPackage>>> GetPackagesAsync(
            string packageId,
            Market market,
            Lang lang,
            bool includeNeutral,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var langList = $"{lang}-{market},{lang}{(includeNeutral ? ",neutral" : "")}";
                var url =
                    $"https://displaycatalog.mp.microsoft.com/v7.0/products/{packageId}?market={market}&languages={langList}";

                HttpClient client = Helpers.GetStoreHttpClient();
                using var response = await client.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return Result<IEnumerable<DCATPackage>>.Failure(
                        new Exception($"Store API returned {response.StatusCode}")
                    );
                }

                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken
                );
                var root = doc.RootElement.GetProperty("Product");

                // --------------------------------------------------
                // PHASE 1: PARSE COMMON DATA
                // --------------------------------------------------
                var localizedProps = root.GetFirstArrayElementOrNull("LocalizedProperties");
                var marketProps = root.GetFirstArrayElementOrNull("MarketProperties");
                var rootProps = root.GetPropertySafe("Properties");
                string productId = root.GetStringSafe("ProductId");
                string title = localizedProps?.GetStringSafe("ProductTitle") ?? "Unknown Title";
                var (shortDesc, desc) = Helpers.ProcessDescriptions(localizedProps);
                string publisher =
                    localizedProps?.GetStringSafe("PublisherName")
                    ?? rootProps.GetStringSafe("PublisherName")
                    ?? "Unknown Publisher";
                string revisionId =
                    root.GetStringSafe("LastModifiedDate")
                    ?? rootProps.GetStringSafe("RevisionId")
                    ?? string.Empty;
                string packageFamily = rootProps.GetStringSafe("PackageFamilyName");
                string packageIdentityName = rootProps.GetStringSafe("PackageIdentityName");

                // Complex Types (Helpers handle defaults)
                var (logo, screenshots) = ParseImages(localizedProps);
                var (rating, ratingCount) = ParseRatings(marketProps, rootProps);

                // --------------------------------------------------
                // PHASE 2: DETERMINE TYPE (Bundle vs Package)
                // --------------------------------------------------
                var displaySku = root.GetProperty("DisplaySkuAvailabilities")[0];
                var sku = displaySku.GetProperty("Sku");
                var skuProps = sku.GetProperty("Properties");
                bool isBundle = skuProps.GetBoolSafe("IsBundle");

                var resultList = new List<DCATPackage>();

                // Base object template
                var basePackage = new DCATPackage
                {
                    ProductId = productId,
                    Title = title,
                    Description = desc,
                    ShortDescription = shortDesc,
                    PublisherName = publisher,
                    RevisionId = revisionId,
                    PackageFamilyName = packageFamily,
                    PackageIdentityName = packageIdentityName,
                    IsBundle = isBundle,
                    Rating = rating,
                    RatingCount = ratingCount,
                    Logo = logo,
                    Screenshots = screenshots,
                    // Nullables
                    PackageFullName = null,
                    WuCategoryId = null,
                    AppVersion = null,
                    Size = null,
                    FrameworkDependencies = null,
                    PlatformDependencies = null,
                };

                if (isBundle)
                {
                    // === BUNDLE LOGIC ===
                    resultList.Add(basePackage);
                }
                else
                {
                    // === PACKAGE LOGIC ===
                    if (
                        skuProps.TryGetProperty("Packages", out var packagesJson)
                        && packagesJson.ValueKind == JsonValueKind.Array
                    )
                    {
                        foreach (var pkgJson in packagesJson.EnumerateArray())
                        {
                            var fulfillment = pkgJson.GetPropertySafe("FulfillmentData");
                            var packageFullName = pkgJson.GetStringSafe("PackageFullName");

                            // Size Logic
                            long? size = pkgJson.GetLongSafe("MaxDownloadSizeInBytes");

                            // Version Logic (Using ulong parsing for WindowsRepresentation)
                            string? versionStr = pkgJson.GetStringSafe("Version");
                            Version? finalVersion = null;
                            if (ulong.TryParse(versionStr, out var vLong) && vLong != 0)
                            {
                                finalVersion = Version.FromWindowsRepresentation(vLong);
                            }

                            // Create specific package instance
                            var pkg = new DCATPackage
                            {
                                // Copy Base Non-Nullables
                                ProductId = basePackage.ProductId,
                                Title = basePackage.Title,
                                Description = basePackage.Description,
                                ShortDescription = basePackage.ShortDescription,
                                PublisherName = basePackage.PublisherName,
                                RevisionId = basePackage.RevisionId,
                                PackageFamilyName = basePackage.PackageFamilyName,
                                PackageIdentityName = basePackage.PackageIdentityName,
                                IsBundle = false, // Explicitly false for items inside
                                Rating = basePackage.Rating,
                                RatingCount = basePackage.RatingCount,
                                Logo = basePackage.Logo,
                                Screenshots = basePackage.Screenshots,

                                // Specific Nullables
                                PackageFullName = packageFullName,
                                WuCategoryId = fulfillment.GetStringSafe("WuCategoryId"),
                                Size = size,
                                AppVersion = finalVersion,
                                PlatformDependencies = ParsePlatforms(pkgJson),
                                FrameworkDependencies = ParseFrameworks(pkgJson),
                            };
                            resultList.Add(pkg);
                        }
                    }
                }

                return Result<IEnumerable<DCATPackage>>.Success(resultList);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<DCATPackage>>.Failure(ex);
            }
        }

        /// <summary>
        /// Bulk Entry Point for multiple Package IDs grouped by Product
        /// </summary>
        public static async Task<Result<IEnumerable<DCATProduct>>> GetMultiplePackagesAsync(
            IEnumerable<string> packageIds,
            Market market,
            Lang lang,
            bool includeNeutral,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var langList = $"{lang}-{market},{lang}{(includeNeutral ? ",neutral" : "")}";
                var bigIds = string.Join(",", packageIds);
                var url =
                    $"https://displaycatalog.mp.microsoft.com/v7/products?market={market}&languages={langList}&bigIds={bigIds}";

                HttpClient client = Helpers.GetStoreHttpClient();
                using var response = await client.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return Result<IEnumerable<DCATProduct>>.Failure(
                        new Exception($"Store API returned {response.StatusCode}")
                    );
                }

                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken
                );

                var resultList = new List<DCATProduct>();

                // Safely check if the "Products" array exists in the response
                if (
                    !doc.RootElement.TryGetProperty("Products", out var productsArray)
                    || productsArray.ValueKind != JsonValueKind.Array
                )
                {
                    return Result<IEnumerable<DCATProduct>>.Success(resultList);
                }

                foreach (var productNode in productsArray.EnumerateArray())
                {
                    // --------------------------------------------------
                    // PHASE 1: PARSE COMMON DATA
                    // --------------------------------------------------
                    var localizedProps = productNode.GetFirstArrayElementOrNull(
                        "LocalizedProperties"
                    );
                    var marketProps = productNode.GetFirstArrayElementOrNull("MarketProperties");
                    var rootProps = productNode.GetPropertySafe("Properties");
                    string productId = productNode.GetStringSafe("ProductId");
                    string title = localizedProps?.GetStringSafe("ProductTitle") ?? "Unknown Title";
                    var (shortDesc, desc) = Helpers.ProcessDescriptions(localizedProps);
                    string publisher =
                        localizedProps?.GetStringSafe("PublisherName")
                        ?? rootProps.GetStringSafe("PublisherName")
                        ?? "Unknown Publisher";
                    string revisionId =
                        productNode.GetStringSafe("LastModifiedDate")
                        ?? rootProps.GetStringSafe("RevisionId")
                        ?? string.Empty;
                    string packageFamily = rootProps.GetStringSafe("PackageFamilyName");
                    string packageIdentityName = rootProps.GetStringSafe("PackageIdentityName");

                    // Complex Types (Helpers handle defaults)
                    var (logo, screenshots) = ParseImages(localizedProps);
                    var (rating, ratingCount) = ParseRatings(marketProps, rootProps);

                    // List to hold the specific packages for this ProductId
                    var packagesForProduct = new List<DCATPackage>();

                    // --------------------------------------------------
                    // PHASE 2: DETERMINE TYPE (Bundle vs Package)
                    // --------------------------------------------------
                    if (
                        !productNode.TryGetProperty("DisplaySkuAvailabilities", out var displaySkus)
                        || displaySkus.GetArrayLength() == 0
                    )
                    {
                        continue; // Skip if no SKUs are available
                    }

                    var displaySku = displaySkus[0];
                    var sku = displaySku.GetProperty("Sku");
                    var skuProps = sku.GetProperty("Properties");
                    bool isBundle = skuProps.GetBoolSafe("IsBundle");

                    // Base object template
                    var basePackage = new DCATPackage
                    {
                        ProductId = productId,
                        Title = title,
                        Description = desc,
                        ShortDescription = shortDesc,
                        PublisherName = publisher,
                        RevisionId = revisionId,
                        PackageFamilyName = packageFamily,
                        PackageIdentityName = packageIdentityName,
                        IsBundle = isBundle,
                        Rating = rating,
                        RatingCount = ratingCount,
                        Logo = logo,
                        Screenshots = screenshots,
                        // Nullables
                        PackageFullName = null,
                        WuCategoryId = null,
                        AppVersion = null,
                        Size = null,
                        FrameworkDependencies = null,
                        PlatformDependencies = null,
                    };

                    if (isBundle)
                    {
                        // === BUNDLE LOGIC ===
                        packagesForProduct.Add(basePackage);
                    }
                    else
                    {
                        // === PACKAGE LOGIC ===
                        if (
                            skuProps.TryGetProperty("Packages", out var packagesJson)
                            && packagesJson.ValueKind == JsonValueKind.Array
                        )
                        {
                            foreach (var pkgJson in packagesJson.EnumerateArray())
                            {
                                var fulfillment = pkgJson.GetPropertySafe("FulfillmentData");
                                var packageFullName = pkgJson.GetStringSafe("PackageFullName");

                                // Size Logic
                                long? size = pkgJson.GetLongSafe("MaxDownloadSizeInBytes");

                                // Version Logic (Using ulong parsing for WindowsRepresentation)
                                string? versionStr = pkgJson.GetStringSafe("Version");
                                Version? finalVersion = null;
                                if (ulong.TryParse(versionStr, out var vLong) && vLong != 0)
                                {
                                    finalVersion = Version.FromWindowsRepresentation(vLong);
                                }

                                // Create specific package instance
                                var pkg = new DCATPackage
                                {
                                    // Copy Base Non-Nullables
                                    ProductId = basePackage.ProductId,
                                    Title = basePackage.Title,
                                    Description = basePackage.Description,
                                    ShortDescription = basePackage.ShortDescription,
                                    PublisherName = basePackage.PublisherName,
                                    RevisionId = basePackage.RevisionId,
                                    PackageFamilyName = basePackage.PackageFamilyName,
                                    PackageIdentityName = basePackage.PackageIdentityName,
                                    IsBundle = false, // Explicitly false for items inside
                                    Rating = basePackage.Rating,
                                    RatingCount = basePackage.RatingCount,
                                    Logo = basePackage.Logo,
                                    Screenshots = basePackage.Screenshots,

                                    // Specific Nullables
                                    PackageFullName = packageFullName,
                                    WuCategoryId = fulfillment.GetStringSafe("WuCategoryId"),
                                    AppVersion = finalVersion,
                                    Size = size,
                                    PlatformDependencies = ParsePlatforms(pkgJson),
                                    FrameworkDependencies = ParseFrameworks(pkgJson),
                                };
                                packagesForProduct.Add(pkg);
                            }
                        }
                    }

                    // Wrap the grouped packages and add to the final list
                    resultList.Add(
                        new DCATProduct { ProductId = productId, Packages = packagesForProduct }
                    );
                }

                return Result<IEnumerable<DCATProduct>>.Success(resultList);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<DCATProduct>>.Failure(ex);
            }
        }

        // -----------------
        // HELPERS
        // -----------------
        private static (Image Logo, List<Image> Screenshots) ParseImages(
            JsonElement? localizedProps
        )
        {
            var defaultLogo = new Image(string.Empty, "Transparent", 0, 0);
            var screens = new List<Image>();

            if (localizedProps == null)
                return (defaultLogo, screens);

            if (
                !localizedProps.Value.TryGetProperty("Images", out var images)
                || images.ValueKind != JsonValueKind.Array
            )
                return (defaultLogo, screens);

            var logoCandidates = new List<Image>();

            foreach (var img in images.EnumerateArray())
            {
                string? uri = img.GetStringSafe("Uri");
                if (string.IsNullOrEmpty(uri))
                    continue;

                if (uri.StartsWith("//"))
                {
                    uri = "https:" + uri;
                }

                string bg = img.GetStringSafe("BackgroundColor") ?? "Transparent";
                if (
                    !bg.StartsWith('#')
                    && !bg.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                )
                    bg = "Transparent";

                int h = img.GetIntSafe("Height");
                int w = img.GetIntSafe("Width");
                string purpose = img.GetStringSafe("ImagePurpose") ?? "";

                var imageObj = new Image(uri, bg, h, w);

                if (string.Equals(purpose, "Screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    screens.Add(imageObj);
                }
                else
                {
                    logoCandidates.Add(imageObj);
                }
            }

            Image finalLogo =
                logoCandidates.LastOrDefault(x => x.Height == 300 && x.Width == 300)
                ?? logoCandidates
                    .Where(x => x.Height == x.Width)
                    .OrderByDescending(x => x.Height)
                    .FirstOrDefault()
                ?? logoCandidates.FirstOrDefault()
                ?? new Image(string.Empty, "Transparent", 0, 0);

            return (finalLogo, screens);
        }

        private static (double Rating, long Count) ParseRatings(
            JsonElement? marketProps,
            JsonElement rootProps
        )
        {
            double rating = 0;
            long count = 0;

            if (
                marketProps != null
                && marketProps.Value.TryGetProperty("UsageData", out var usageData)
                && usageData.ValueKind == JsonValueKind.Array
            )
            {
                JsonElement? targetUsage = null;
                foreach (var u in usageData.EnumerateArray())
                {
                    if (
                        string.Equals(
                            u.GetStringSafe("AggregateTimeSpan"),
                            "AllTime",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        targetUsage = u;
                        break;
                    }
                }
                targetUsage ??= (
                    usageData.GetArrayLength() > 0 ? usageData[0] : (JsonElement?)null
                );

                if (targetUsage.HasValue)
                {
                    rating = targetUsage.Value.GetDoubleSafe("AverageRating");
                    count = targetUsage.Value.GetLongSafe("RatingCount");
                }
            }

            if (rating == 0)
                rating = rootProps.GetDoubleSafe("RatingAverage");
            if (count == 0)
                count = rootProps.GetLongSafe("RatingCount");

            return (rating, count);
        }

        private static IEnumerable<PlatformDependency>? ParsePlatforms(JsonElement packageJson)
        {
            if (
                !packageJson.TryGetProperty("PlatformDependencies", out var deps)
                || deps.ValueKind != JsonValueKind.Array
            )
                return null;

            var list = new List<PlatformDependency>();
            foreach (var d in deps.EnumerateArray())
            {
                var pName = d.GetStringSafe("PlatformName")?.ToLowerInvariant();
                var minVer = d.GetProperty("MinVersion").GetUInt64();

                list.Add(
                    new PlatformDependency
                    {
                        MinVersion = Version.FromWindowsRepresentation(minVer),
                        Platform = pName switch
                        {
                            "windows.desktop" => DeviceFamily.Desktop,
                            "windows.server" => DeviceFamily.Server,
                            "windows.iotuap" => DeviceFamily.IoTUAP,
                            "windows.iot" => DeviceFamily.Iot,
                            "windows.team" => DeviceFamily.Team,
                            "windows.holographic" => DeviceFamily.Holographic,
                            "windows.mobile" => DeviceFamily.Mobile,
                            "windows.core" => DeviceFamily.Core,
                            "windows.xbox" => DeviceFamily.Xbox,
                            "windows.universal" => DeviceFamily.Universal,
                            _ => DeviceFamily.Unknown,
                        },
                    }
                );
            }
            return list.Any() ? list : null;
        }

        private static IEnumerable<FrameworkDependency>? ParseFrameworks(JsonElement packageJson)
        {
            if (
                !packageJson.TryGetProperty("FrameworkDependencies", out var deps)
                || deps.ValueKind != JsonValueKind.Array
            )
                return null;

            var list = new List<FrameworkDependency>();
            foreach (var d in deps.EnumerateArray())
            {
                list.Add(
                    new FrameworkDependency
                    {
                        PackageIdentity = d.GetStringSafe("PackageIdentity") ?? "Unknown",
                        MinVersion = Version.FromWindowsRepresentation(
                            d.GetProperty("MinVersion").GetUInt64()
                        ),
                    }
                );
            }
            return list.Any() ? list : null;
        }
    }
}
