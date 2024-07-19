using StoreListings.Library.Internal;
using System.Text.Json;

namespace StoreListings.Library;

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

    public required string WuCategoryId { get; set; }

    public required Version Version { get; set; }

    public required int PackageRank { get; set; }

    public required string PackageIdentity { get; set; }

    public required IEnumerable<FrameworkDependency> FrameworkDependencies { get; set; }

    public required IEnumerable<PlatformDependency> PlatformDependencies { get; set; }

    public static async Task<Result<IEnumerable<DCATPackage>>> GetPackagesAsync(string packageId, Market market, Lang lang, bool includeNeutral)
    {
        string langList = $"{lang}-{market},{lang}{(includeNeutral ? ",neutral" : "")}";
        string url = $"https://displaycatalog.mp.microsoft.com/v7.0/products/{packageId}?market={market}&languages={langList}";
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            JsonDocument? json = null;
            try
            {
                json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            }
            catch
            {
                response.EnsureSuccessStatusCode();
            }

            JsonElement root = json!.RootElement.GetProperty("Product");
            if (!response.IsSuccessStatusCode)
            {
                return Result<IEnumerable<DCATPackage>>.Failure(new Exception(root.GetProperty("message").GetString()));
            }

            JsonElement packagesJson = root
                .GetProperty("DisplaySkuAvailabilities")[0]
                .GetProperty("Sku")
                .GetProperty("Properties")
                .GetProperty("Packages");

            List<DCATPackage> packages = new(packagesJson.GetArrayLength());
            for (int i = 0; i < packagesJson.GetArrayLength(); i++)
            {
                JsonElement packageJson = packagesJson[i];
                int platformDependenciesCount = packageJson.GetProperty("PlatformDependencies").GetArrayLength();
                List<PlatformDependency> platformDependencies = new(platformDependenciesCount);
                for (int j = 0; j < platformDependenciesCount; j++)
                {
                    JsonElement platformDependencyJson = packageJson.GetProperty("PlatformDependencies")[j];
                    DeviceFamily platform = platformDependencyJson.GetProperty("PlatformName").GetString()!.ToLower() switch
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
                        // TODO: Add windows.windows8x and windows.windowsphone8x
                        _ => DeviceFamily.Unknown
                    };
                    platformDependencies.Add(new PlatformDependency
                    {
                        Platform = platform,
                        MinVersion = Version.FromWindowsRepresentation(platformDependencyJson.GetProperty("MinVersion").GetUInt64())
                    });
                }

                int frameworkDependenciesCount = packageJson.GetProperty("FrameworkDependencies").GetArrayLength();
                List<FrameworkDependency> frameworkDependencies = new(frameworkDependenciesCount);
                for (int j = 0; j < frameworkDependenciesCount; j++)
                {
                    JsonElement frameworkDependencyJson = packageJson.GetProperty("FrameworkDependencies")[j];
                    frameworkDependencies.Add(new FrameworkDependency
                    {
                        PackageIdentity = frameworkDependencyJson.GetProperty("PackageIdentity").GetString()!,
                        MinVersion = Version.FromWindowsRepresentation(frameworkDependencyJson.GetProperty("MinVersion").GetUInt64())
                    });
                }

                packages.Add(new DCATPackage
                {
                    PackageRank = packageJson.GetProperty("PackageRank").GetInt32(),
                    WuCategoryId = packageJson.GetProperty("FulfillmentData").GetProperty("WuCategoryId").GetString()!,
                    Version = Version.FromWindowsRepresentation(ulong.Parse(packageJson.GetProperty("Version").GetString()!)),
                    FrameworkDependencies = frameworkDependencies,
                    PlatformDependencies = platformDependencies,
                    PackageIdentity = root.GetProperty("Properties").GetProperty("PackageIdentityName").GetString()!
                });
            }

            return Result<IEnumerable<DCATPackage>>.Success(packages);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<DCATPackage>>.Failure(ex);
        }
    }
}