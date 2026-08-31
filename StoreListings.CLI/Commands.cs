using System.Linq;
using ConsoleAppFramework;
using StoreListings.Library;
using static StoreListings.CLI.Helpers;

namespace StoreListings.CLI;

public class Commands
{
    /// <summary>
    /// Query products using keyword from Microsoft Store.
    /// </summary>
    /// <param name="query">The keyword to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task Query(
        [Argument] string query,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDQuery> result = await StoreEdgeFDQuery.GetSearchProduct(
            query,
            deviceFamily,
            market,
            language,
            cancellationToken: cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            List<Card> cards = result.Value.Cards;
            foreach (var card in cards)
            {
                WriteField("Product ID", card.ProductId);
                WriteField("Title", card.Title);
                if (card.DisplayPrice != null)
                {
                    WriteField("Display price", card.DisplayPrice);
                }
                if (card.AverageRating != null)
                {
                    WriteField("Average rating", card.AverageRating.ToString());
                }
                WriteField("Image", card.Image.Url);
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query bundle details from Microsoft Store.
    /// </summary>
    /// <param name="productId">The product ID to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryBundles(
        [Argument] string productId,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDQuery> result = await StoreEdgeFDQuery.GetBundles(
            productId,
            deviceFamily,
            market,
            language,
            cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            List<Card> cards = result.Value.Cards;
            foreach (var card in cards)
            {
                WriteField("Product ID", card.ProductId);
                WriteField("Title", card.Title);
                if (card.DisplayPrice != null)
                {
                    WriteField("Display price", card.DisplayPrice);
                }
                if (card.AverageRating != null)
                {
                    WriteField("Average rating", card.AverageRating.ToString());
                }
                WriteField("Image", card.Image.Url);
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query recommendations from Microsoft Store based on category.
    /// </summary>
    /// <param name="category">-c, The category on which recommendations should be fetched.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryRecommendations(
        CancellationToken cancellationToken,
        Category category = Category.TopFree,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDQuery> result = await StoreEdgeFDQuery.GetRecommendations(
            category,
            deviceFamily,
            market,
            language,
            cancellationToken: cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            List<Card> cards = result.Value.Cards;
            foreach (var card in cards)
            {
                WriteField("Product ID", card.ProductId);
                WriteField("Title", card.Title);
                if (card.DisplayPrice != null)
                {
                    WriteField("Display price", card.DisplayPrice);
                }
                if (card.AverageRating != null)
                {
                    WriteField("Average rating", card.AverageRating.ToString());
                }
                WriteField("Image", card.Image.Url);
            }
        }
        else
        {
            Console.WriteLine(result.Exception.ToString());
        }
    }

    /// <summary>
    /// Query suggestions for a keyword from Microsoft Store.
    /// </summary>
    /// <param name="query">The keyword to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QuerySuggestions(
        [Argument] string query,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDSuggestions> result = await StoreEdgeFDSuggestions.GetSearchSuggestion(
            query,
            deviceFamily,
            market,
            language,
            cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            List<Card> cards = result.Value.Cards;
            List<string> suggestions = result.Value.Suggestions;
            WriteField("Suggestions", string.Join(", ", suggestions));
            foreach (var card in cards)
            {
                WriteField("Product ID", card.ProductId);
                WriteField("Title", card.Title);
                if (card.DisplayPrice != null)
                {
                    WriteField("Display price", card.DisplayPrice);
                }
                if (card.AverageRating != null)
                {
                    WriteField("Average rating", card.AverageRating.ToString());
                }
                WriteField("Image", card.Image.Url);
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query a product ID on Microsoft Store.
    /// </summary>
    /// <param name="productId">The product ID of the product to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryProduct(
        [Argument] string productId,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDProduct> result = await StoreEdgeFDProduct.GetProductAsync(
            productId,
            deviceFamily,
            market,
            language,
            cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            StoreEdgeFDProduct product = result.Value;
            WriteField("Product ID", product.ProductId);
            WriteField("Title", product.Title);
            WriteField("Logo", product.Logo.Url);
            WriteField("Screenshots", product.Screenshots.Count.ToString());
            foreach (var screenshot in product.Screenshots)
            {
                Console.WriteLine(screenshot.Url);
            }
            WriteField("Revision ID", product.RevisionId);
            WriteField("Average rating", product.Rating.ToString());
            WriteField("Rating count", product.RatingCount.ToString());
            WriteField("Size", product.Size.ToString());
            if (product.ShortDescription is not null)
                WriteField("Short Description", product.ShortDescription);
            if (product.Description is not null)
                WriteField("Description", product.Description);
            WriteField("Publisher", product.PublisherName);
            WriteField("Installer Type", product.InstallerType.ToString());
            WriteField("Is Bundle", product.IsBundle.ToString());
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// List download links for a product.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    /// <param name="flightRing">-r, The flight ring (i.e. Retail, External, Internal).</param>
    /// <param name="flightingBranchName">-b, The flighting branch name (i.e. Retail, CanaryChannel, Dev, Beta, ReleasePreview).</param>
    /// <param name="currentBranch">-c, The current OS branch (i.e. rs_prerelease, ge_release, ni_release, co_release, vb_release)</param>
    /// <param name="OSVersion">-v, The current OS version (i.e. 10.0.26100.0). Leave to null for 10.0.26100.0.</param>
    public async Task Download(
        [Argument] string productId,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en,
        string flightRing = "Retail",
        string flightingBranchName = "Retail",
        Library.Version? OSVersion = null,
        string currentBranch = "ge_release"
    )
    {
        if (OSVersion is null)
            OSVersion = new(10, 0, 26100, 0);

        WriteLoadingProgressBar();
        Result<StoreEdgeFDProduct> result = await StoreEdgeFDProduct.GetProductAsync(
            productId,
            deviceFamily,
            market,
            language,
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            WriteError(result.Exception, "querying the product ID");
            return;
        }

        StoreEdgeFDProduct product = result.Value;
        switch (product.InstallerType)
        {
            case InstallerType.Packaged:
                Result<IEnumerable<DCATPackage>> packageResult = await DCATPackage.GetPackagesAsync(
                    productId,
                    market,
                    language,
                    true
                );
                if (!packageResult.IsSuccess)
                {
                    WriteError(packageResult.Exception, "querying packages");
                    return;
                }

                Result<FE3Handler.Cookie> cookieResult = await FE3Handler.GetCookieAsync(
                    cancellationToken
                );
                if (!cookieResult.IsSuccess)
                {
                    WriteError(cookieResult.Exception, "getting Windows Update cookies");
                    return;
                }
                Result<FE3Handler.SyncUpdatesResponse> fe3sync = await FE3Handler.SyncUpdatesAsync(
                    cookieResult.Value,
                    packageResult.Value.First().WuCategoryId,
                    language,
                    market,
                    currentBranch,
                    flightRing,
                    flightingBranchName,
                    OSVersion.Value,
                    DeviceFamily.Desktop,
                    cancellationToken
                );
                if (!fe3sync.IsSuccess)
                {
                    WriteError(fe3sync.Exception, "syncing updates");
                    return;
                }

                List<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)> updatesAndUrl =
                    new(fe3sync.Value.Updates.Count());

                foreach (FE3Handler.SyncUpdatesResponse.Update update in fe3sync.Value.Updates)
                {
                    var fileUrlResult = await FE3Handler.GetPackageDownloadInfo(
                        fe3sync.Value.NewCookie,
                        update.UpdateID,
                        update.RevisionNumber,
                        update.Digest,
                        language,
                        market,
                        currentBranch,
                        flightRing,
                        flightingBranchName,
                        OSVersion.Value,
                        deviceFamily,
                        cancellationToken
                    );
                    if (!fileUrlResult.IsSuccess)
                    {
                        WriteError(
                            fileUrlResult.Exception,
                            $"getting file URL for file {update.FileName}"
                        );
                        return;
                    }
                    updatesAndUrl.Add((update, fileUrlResult.Value.Package.Url));
                }

                int printedPackages = 0;

                // FE3's bundle tree is the source of truth: each binary version declares
                // its own exact framework dependencies. Map every package URL by UpdateID
                // so resolved dependencies can be looked up.
                Dictionary<string, string> urlByUpdateId = new(StringComparer.OrdinalIgnoreCase);
                foreach (var (depUpdate, depUrl) in updatesAndUrl)
                    urlByUpdateId.TryAdd(depUpdate.UpdateID, depUrl);

                foreach (
                    var update in updatesAndUrl
                        .Where(f => !f.Update.IsFramework)
                        .OrderByDescending(f => f.Update.Version)
                )
                {
                    if (
                        !update.Update.TargetPlatforms.Any(f =>
                            (f.Family == deviceFamily || f.Family == DeviceFamily.Universal)
                            && f.MinVersion <= OSVersion.Value
                        )
                    )
                        continue;

                    printedPackages++;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(update.Update.Version);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Main package ({update.Update.FileName}):");
                    Console.ResetColor();
                    Console.WriteLine(update.Url);
                    Console.WriteLine();

                    // Resolve this exact binary's dependencies from the FE3 bundle tree.
                    IReadOnlyList<FE3Handler.SyncUpdatesResponse.Update> dependencies =
                        fe3sync.Value.ResolveDependencies(update.Update);

                    if (dependencies.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("Dependencies:");
                        Console.WriteLine();

                        foreach (FE3Handler.SyncUpdatesResponse.Update dep in dependencies)
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine(dep.FileName);
                            Console.ResetColor();
                            if (urlByUpdateId.TryGetValue(dep.UpdateID, out string? dependencyUrl))
                                Console.WriteLine(dependencyUrl);
                            else
                                Console.WriteLine($"// missing dependency {dep.UpdateID}");
                        }
                        Console.WriteLine();
                        Console.ResetColor();
                    }

                    Console.WriteLine();
                }

                if (printedPackages == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No applicable packages were found for your OS options.");
                    Console.ResetColor();
                }
                break;

            case InstallerType.Unpackaged:
                var unpackagedResult = await StoreEdgeFDProduct.GetUnpackagedInstall(
                    productId,
                    market,
                    cancellationToken
                );
                if (!unpackagedResult.IsSuccess)
                {
                    WriteError(unpackagedResult.Exception, "getting unpackaged install");
                    return;
                }
                foreach (
                    var (
                        InstallerUrl,
                        FileName,
                        InstallerSwitches,
                        Version,
                        InstallerSha256,
                        arch,
                        locale
                    ) in unpackagedResult.Value
                )
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(Version);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Installer file ({FileName}):");
                    Console.ResetColor();
                    Console.WriteLine(InstallerUrl);
                    Console.WriteLine();
                    Console.WriteLine("Silent switches:");
                    Console.WriteLine(InstallerSwitches);
                    Console.WriteLine();
                    Console.WriteLine("SHA256:");
                    Console.WriteLine(InstallerSha256);
                    Console.WriteLine();
                    Console.WriteLine("arch:");
                    Console.WriteLine(arch);
                    Console.WriteLine();
                    Console.WriteLine("Locale:");
                    Console.WriteLine(locale);
                }

                break;

            case InstallerType.Unknown:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The product has an unsupported installer type.");
                Console.ResetColor();
                break;
        }

        HideProgressBar();
    }

    /// <summary>
    /// Query packages for a product from the Display Catalog.
    /// </summary>
    /// <param name="productId">The product ID to query.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryPackages(
        [Argument] string productId,
        CancellationToken cancellationToken,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<IEnumerable<DCATPackage>> result = await DCATPackage.GetPackagesAsync(
            productId,
            market,
            language,
            true
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            foreach (var package in result.Value)
            {
                WriteField("Product ID", package.ProductId ?? "Missing");
                WriteField("Title", package.Title ?? "Missing");
                WriteField("Short Description", package.ShortDescription ?? "Missing");
                WriteField("Description", package.Description ?? "Missing");
                WriteField("Publisher", package.PublisherName ?? "Missing");
                WriteField("Revision ID", package.RevisionId ?? "Missing");
                WriteField("Average rating", package.Rating.ToString() ?? "Missing");
                WriteField("Rating count", package.RatingCount.ToString() ?? "Missing");
                WriteField("Size", package.Size?.ToString() ?? "Missing");
                WriteField("Is Bundle", package.IsBundle.ToString());
                WriteField("Package Family Name", package.PackageFamilyName ?? "Missing");
                WriteField("Package Name", package.PackageFullName ?? "Missing");
                WriteField("Logo", package.Logo?.Url ?? "Missing");
                WriteField("Screenshots", package.Screenshots.Count.ToString());
                foreach (var screenshot in package.Screenshots)
                {
                    Console.WriteLine(screenshot.Url);
                }
                WriteField("Version", package.AppVersion.ToString());
                WriteField("WuCategoryId", package.WuCategoryId);
                WriteField(
                    "Platform Dependencies",
                    string.Join(
                        ", ",
                        (
                            package.PlatformDependencies
                            ?? Enumerable.Empty<DCATPackage.PlatformDependency>()
                        ).Select(p => $"{p.Platform}: {p.MinVersion}")
                    )
                );
                WriteField(
                    "Framework Dependencies",
                    string.Join(
                        ", ",
                        (
                            package.FrameworkDependencies
                            ?? Enumerable.Empty<DCATPackage.FrameworkDependency>()
                        ).Select(f => $"{f.PackageIdentity}: {f.MinVersion}")
                    )
                );
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query a product page details from Microsoft Store.
    /// </summary>
    /// <param name="productId">The product ID of the product to query.</param>
    /// <param name="architecture">The architecture (e.g. x64, x86, arm64).</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryPage(
        [Argument] string productId,
        CancellationToken cancellationToken,
        StoreEdgeFDArch architecture = StoreEdgeFDArch.X64,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDPage> result = await StoreEdgeFDPage.GetProductAsync(
            productId,
            architecture,
            market,
            language,
            cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            StoreEdgeFDPage page = result.Value;
            WriteField("Product ID", page.ProductId);
            WriteField("Title", page.Title);
            WriteField("Logo", page.Logo.Url);
            WriteField("Screenshots", page.Screenshots.Count.ToString());
            foreach (var screenshot in page.Screenshots)
            {
                Console.WriteLine(screenshot.Url);
            }
            WriteField("Short Description", page.ShortDescription);
            WriteField("Description", page.Description);
            WriteField("Publisher", page.PublisherName);
            WriteField("Average rating", page.Rating.ToString());
            WriteField("Rating count", page.RatingCount.ToString());
            if (page.Size.HasValue)
                WriteField("Size", page.Size.Value.ToString());
            WriteField("Installer Type", page.InstallerType.ToString());
            if (page.PackageFamilyName is not null)
                WriteField("Package Family Name", page.PackageFamilyName);
            if (page.LastUpdateDate.HasValue)
                WriteField("Last Update Date", page.LastUpdateDate.Value.ToString());
            if (page.Version is not null)
                WriteField("Version", page.Version);
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query multiple products by ID type from Microsoft Store.
    /// </summary>
    /// <param name="idType">The type of product ID (e.g. ProductId, LegacyId, etc).</param>
    /// <param name="productIds">A list of product IDs to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryProductsByIdType(
        [Argument] StoreIdType idType,
        [Argument] string productIds,
        CancellationToken cancellationToken,
        DeviceFamily deviceFamily = DeviceFamily.Desktop,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        var productIdList = productIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        WriteLoadingProgressBar();
        var result = await StoreEdgeFDProduct.GetProductsByIdTypeAsync(
            productIdList,
            idType,
            deviceFamily,
            market,
            language,
            cancellationToken
        );
        HideProgressBar();
        if (result.IsSuccess)
        {
            foreach (var product in result.Value)
            {
                WriteField("Product ID", product.ProductId);
                WriteField("Title", product.Title);
                WriteField("Publisher", product.PublisherName);
                WriteField("Revision ID", product.RevisionId);
                WriteField("Average rating", product.Rating.ToString());
                WriteField("Rating count", product.RatingCount.ToString());
                WriteField("Size", product.Size.ToString());
                WriteField("Is Bundle", product.IsBundle.ToString());
                WriteField("Installer Type", product.InstallerType.ToString());
                WriteField("Logo", product.Logo.Url);
                WriteField("Screenshots", product.Screenshots.Count.ToString());
                foreach (var screenshot in product.Screenshots)
                {
                    Console.WriteLine(screenshot.Url);
                }
                if (!string.IsNullOrEmpty(product.ShortDescription))
                    WriteField("Short Description", product.ShortDescription);
                if (!string.IsNullOrEmpty(product.Description))
                    WriteField("Description", product.Description);
                if (!string.IsNullOrEmpty(product.PackageFamilyName))
                    WriteField("Package Family Name", product.PackageFamilyName);
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }

    /// <summary>
    /// Query multiple packages from the Display Catalog using a comma-separated list of package IDs.
    /// </summary>
    /// <param name="packageIds">A list of package IDs to query (comma-separated).</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task QueryMultiplePackages(
        [Argument] string packageIds,
        CancellationToken cancellationToken,
        Market market = Market.US,
        Lang language = Lang.en
    )
    {
        var packageIdList = packageIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        WriteLoadingProgressBar();
        Result<IEnumerable<DCATPackage.DCATProduct>> result =
            await DCATPackage.GetMultiplePackagesAsync(
                packageIdList,
                market,
                language,
                true,
                cancellationToken
            );
        HideProgressBar();
        if (result.IsSuccess)
        {
            var products = result.Value.ToList();
            var productCount = products.Count;
            var packageCount = products
                .SelectMany(p => p.Packages ?? Enumerable.Empty<DCATPackage>())
                .Count();
            Console.WriteLine($"products: {productCount}, packages: {packageCount}");

            foreach (var product in products)
            {
                WriteField("Product ID", product.ProductId ?? "Missing");
                foreach (var package in product.Packages ?? Enumerable.Empty<DCATPackage>())
                {
                    WriteField("Title", package.Title ?? "Missing");
                    WriteField("Short Description", package.ShortDescription ?? "Missing");
                    WriteField("Description", package.Description ?? "Missing");
                    WriteField("Publisher", package.PublisherName ?? "Missing");
                    WriteField("Revision ID", package.RevisionId ?? "Missing");
                    WriteField("Average rating", package.Rating.ToString() ?? "Missing");
                    WriteField("Rating count", package.RatingCount.ToString() ?? "Missing");
                    WriteField("Size", package.Size?.ToString() ?? "Missing");
                    WriteField("Is Bundle", package.IsBundle.ToString());
                    WriteField("Package Family Name", package.PackageFamilyName ?? "Missing");
                    WriteField("Package Name", package.PackageFullName ?? "Missing");
                    WriteField("Logo", package.Logo?.Url ?? "Missing");
                    WriteField("Screenshots", package.Screenshots.Count.ToString());
                    foreach (var screenshot in package.Screenshots)
                    {
                        Console.WriteLine(screenshot.Url);
                    }
                    WriteField("Version", package.AppVersion?.ToString() ?? "Missing");
                    WriteField("WuCategoryId", package.WuCategoryId);
                    WriteField(
                        "Platform Dependencies",
                        string.Join(
                            ", ",
                            (
                                package.PlatformDependencies
                                ?? Enumerable.Empty<DCATPackage.PlatformDependency>()
                            ).Select(p => $"{p.Platform}: {p.MinVersion}")
                        )
                    );
                    WriteField(
                        "Framework Dependencies",
                        string.Join(
                            ", ",
                            (
                                package.FrameworkDependencies
                                ?? Enumerable.Empty<DCATPackage.FrameworkDependency>()
                            ).Select(f => $"{f.PackageIdentity}: {f.MinVersion}")
                        )
                    );
                    Console.WriteLine();
                }
            }
        }
        else
        {
            Console.WriteLine(result.Exception);
        }
    }
}
