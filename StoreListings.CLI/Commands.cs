using ConsoleAppFramework;
using StoreListings.Library;
using static StoreListings.CLI.Helpers;

namespace StoreListings.CLI;

public class Commands
{
    /// <summary>
    /// Query a product ID on Microsoft Store.
    /// </summary>
    /// <param name="productId">The product ID of the product to query.</param>
    /// <param name="deviceFamily">-d, The device family.</param>
    /// <param name="market">-m, The store market/region to query from.</param>
    /// <param name="language">-l, The language, for listings that use localization.</param>
    public async Task Query([Argument] string productId, CancellationToken cancellationToken, DeviceFamily deviceFamily = DeviceFamily.Desktop, Market market = Market.US, Lang language = Lang.en)
    {
        WriteLoadingProgressBar();
        Result<StoreEdgeFDProduct> result = await StoreEdgeFDProduct.GetProductAsync(productId, deviceFamily, market, language, cancellationToken);
        HideProgressBar();
        if (result.IsSuccess)
        {
            StoreEdgeFDProduct product = result.Value;
            WriteField("Product ID", product.ProductId);
            WriteField("Title", product.Title);
            if (product.Description is not null)
                WriteField("Description", product.Description);
            WriteField("Publisher", product.PublisherName);
            WriteField("Installer Type", product.InstallerType.ToString());
        }
        else
        {
            WriteError(result.Exception, "querying the product ID");
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
        string flightingBranchName = "",
        Library.Version? OSVersion = null,
        string currentBranch = "ge_release")
    {
        if (OSVersion is null)
            OSVersion = new(10, 0, 26100, 0);

        WriteLoadingProgressBar();
        Result<StoreEdgeFDProduct> result = await StoreEdgeFDProduct.GetProductAsync(productId, deviceFamily, market, language, cancellationToken);
        if (!result.IsSuccess)
        {
            WriteError(result.Exception, "querying the product ID");
            return;
        }

        StoreEdgeFDProduct product = result.Value;
        switch (product.InstallerType)
        {
            case InstallerType.Packaged:
                Result<IEnumerable<DCATPackage>> packageResult = await DCATPackage.GetPackagesAsync(productId, market, language, true);
                if (!packageResult.IsSuccess)
                {
                    WriteError(packageResult.Exception, "querying packages");
                    return;
                }

                if (!packageResult.Value.Any(f => f.PlatformDependencies.Any(f => f.MinVersion <= OSVersion.Value)))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No applicable packages were found for your OS options.");
                    Console.ResetColor();
                    HideProgressBar();
                    return;
                }

                Result<FE3Handler.Cookie> cookieResult = (await FE3Handler.GetCookieAsync(cancellationToken));
                if (!cookieResult.IsSuccess)
                {
                    WriteError(cookieResult.Exception, "getting Windows Update cookies");
                    return;
                }
                Result<FE3Handler.SyncUpdatesResponse> fe3sync = await FE3Handler.SyncUpdatesAsync(cookieResult.Value, packageResult.Value.First().WuCategoryId, language, market, currentBranch, flightRing, flightingBranchName, OSVersion.Value, DeviceFamily.Desktop, cancellationToken);
                if (!fe3sync.IsSuccess)
                {
                    WriteError(fe3sync.Exception, "syncing updates");
                    return;
                }

                List<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)> updatesAndUrl = new(fe3sync.Value.Updates.Count());

                foreach (FE3Handler.SyncUpdatesResponse.Update update in fe3sync.Value.Updates)
                {
                    Result<string> fileUrlResult = await FE3Handler.GetFileUrl(fe3sync.Value.NewCookie, update.UpdateID, update.RevisionNumber, update.Digest, language, market, currentBranch, flightRing, flightingBranchName, OSVersion.Value, deviceFamily, cancellationToken);
                    if (!fileUrlResult.IsSuccess)
                    {
                        WriteError(fileUrlResult.Exception, $"getting file URL for file {update.FileName}");
                        return;
                    }
                    updatesAndUrl.Add((update, fileUrlResult.Value));
                }

                int printedPackages = 0;

                foreach ((FE3Handler.SyncUpdatesResponse.Update Update, string Url) update in updatesAndUrl.Where(f => !f.Update.IsFramework).OrderByDescending(f => f.Update.Version))
                {
                    if (!update.Update.TargetPlatforms.Any(f => (f.Family == deviceFamily || f.Family == DeviceFamily.Universal) && f.MinVersion <= OSVersion.Value))
                        continue;

                    bool frameworkDependencyApplicable = true;

                    DCATPackage? package = packageResult.Value.FirstOrDefault(f => 
                        f.PackageIdentity.Equals(update.Update.PackageIdentityName, StringComparison.OrdinalIgnoreCase) &&
                        f.Version == update.Update.Version);

                    IEnumerable<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)> dependencyList = Array.Empty<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)>();

                    if (package is not null)
                    {
                        dependencyList = new List<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)>(package.PlatformDependencies.Count() * 4);

                        foreach (DCATPackage.FrameworkDependency dependency in package.FrameworkDependencies)
                        {
                            var applicableDependencyFiles = updatesAndUrl.Where(
                                dep =>
                                dep.Update.PackageIdentityName.Equals(dependency.PackageIdentity, StringComparison.OrdinalIgnoreCase) &&
                                dep.Update.Version >= dependency.MinVersion &&
                                dep.Update.TargetPlatforms.Any(
                                    platform =>
                                    platform.MinVersion <= OSVersion.Value &&
                                    (platform.Family == DeviceFamily.Universal || platform.Family == deviceFamily)));

                            if (!applicableDependencyFiles.Any())
                            {
                                // The package has unapplicable dependency (meaning it's impossible to install the dependency), ignore the file;
                                frameworkDependencyApplicable = false;
                                break;
                            }

                            // Get the latest version of the dependency
                            ((List<(FE3Handler.SyncUpdatesResponse.Update Update, string Url)>)dependencyList).AddRange(applicableDependencyFiles.GroupBy(f => f.Update.Version).OrderByDescending(f => f.Key).First());
                        }

                        if (!frameworkDependencyApplicable)
                            continue; // There are unapplicable dependencies, ignore the file.
                    }

                    printedPackages++;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(update.Update.Version);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Main package ({update.Update.FileName}):");
                    Console.ResetColor();
                    Console.WriteLine(update.Url);
                    Console.WriteLine();

                    if (package is not null)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("Dependencies:");
                        Console.WriteLine();

                        foreach ((FE3Handler.SyncUpdatesResponse.Update Update, string Url) dependencyFile in dependencyList)
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine(dependencyFile.Update.FileName);
                            Console.ResetColor();
                            Console.WriteLine(dependencyFile.Url);
                        }
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed to get dependencies for version {update.Update.Version}");
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
                Result<(string InstallerUrl, string InstallerSwitches)> unpackagedResult = await product.GetUnpackagedInstall(market, language, cancellationToken);
                if (!unpackagedResult.IsSuccess)
                {
                    WriteError(unpackagedResult.Exception, "getting unpackaged install");
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Installer URL:");
                Console.ResetColor();
                Console.WriteLine(unpackagedResult.Value.InstallerUrl);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Installer silent switches:");
                Console.ResetColor();
                Console.WriteLine(unpackagedResult.Value.InstallerSwitches);
                break;

            case InstallerType.Unknown:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The product has an unsupported installer type.");
                Console.ResetColor();
                break;
        }

        HideProgressBar();
    }
}