using StoreListings.Library.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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
    /// The listing description, if available.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The publisher name.
    /// </summary>
    public required string PublisherName { get; set; }

    /// <summary>
    /// The installer type.
    /// </summary>
    public required InstallerType InstallerType { get; set; }

    [SetsRequiredMembers]
    private StoreEdgeFDProduct(string productId, string title, string? description, string publisherName, InstallerType installerType)
    {
        ProductId = productId;
        Title = title;
        Description = description;
        PublisherName = publisherName;
        InstallerType = installerType;
    }

    public static async Task<Result<StoreEdgeFDProduct>> GetProductAsync(string productId, DeviceFamily deviceFamily, Market market, Lang language, CancellationToken cancellationToken = default)
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            string url = $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{productId}?market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}";
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            JsonDocument? json = null;
            try
            {
                json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(), cancellationToken: cancellationToken);
            }
            catch
            {
                // Definitely an error.
                response.EnsureSuccessStatusCode();
            }

            // We don't know yet if the response is an error, but we do know the response is in JSON.
            using JsonDocument jsondoc = json!;

            if (!response.IsSuccessStatusCode)
            {
                // Error.
                return Result<StoreEdgeFDProduct>.Failure(new Exception(jsondoc.RootElement.GetProperty("message").GetString()));
            }

            JsonElement payloadElement = jsondoc.RootElement.GetProperty("Payload");

            string title = payloadElement.GetProperty("Title").GetString()!;
            string publisherName = payloadElement.GetProperty("PublisherName").GetString()!;
            // Future-proofing.
            string prodId = payloadElement.GetProperty("ProductId").GetString()!;

            string? description = null;
            if (payloadElement.TryGetProperty("ShortDescription", out JsonElement shortDesJson) &&
                shortDesJson.GetString() is string shortDes &&
                !string.IsNullOrEmpty(shortDes))
            {
                description = shortDes;
            }
            else if (payloadElement.TryGetProperty("Description", out JsonElement desJson) &&
                desJson.GetString() is string des &&
                !string.IsNullOrEmpty(des))
            {
                int index = des.IndexOf(Environment.NewLine);
                description = index == -1 ? des : des[..index];
            }

            InstallerType installerType = payloadElement.GetProperty("Installer").GetProperty("Type").GetString()! switch
            {
                "WindowsUpdate" => InstallerType.Packaged,
                "WPM" => InstallerType.Unpackaged,
                // GamingServices?
                _ => InstallerType.Unknown
            };

            return Result<StoreEdgeFDProduct>.Success(new(prodId, title, description, publisherName, installerType));
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDProduct>.Failure(ex);
        }
    }

    public async Task<Result<(string InstallerUrl, string InstallerSwitches)>> GetUnpackagedInstall(Market market, Lang language, CancellationToken cancellationToken = default)
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        try
        {
            string url = $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/packageManifests/{ProductId}";
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            JsonDocument? json = null;
            try
            {
                json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(), cancellationToken: cancellationToken);
            }
            catch
            {
                // Definitely an error.
                response.EnsureSuccessStatusCode();
            }
            if (!response.IsSuccessStatusCode)
            {
                return Result<(string InstallerUrl, string InstallerSwitches)>.Failure(new Exception(json!.RootElement.GetProperty("message").GetString()));
            }
            var installers = json!.RootElement.GetProperty("Data").GetProperty("Versions")[0].GetProperty("Installers");
            List<(string InstallerUrl, string InstallerSwitches, uint Priority)> installersList = new(2);
            for (int i = 0; i < installers.GetArrayLength(); i++)
            {
                JsonElement installer = installers[i];
                string locale = installer.GetProperty("InstallerLocale").GetString()!;
                if (!locale.StartsWith(language.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;
                int priority = locale.Equals($"{language}-{market}", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                installersList.Add((installer.GetProperty("InstallerUrl").GetString()!, installer.GetProperty("InstallerSwitches").GetProperty("Silent").GetString()!, (uint)priority));
            }
            if (installersList.Count == 0)
            {
                return Result<(string InstallerUrl, string InstallerSwitches)>.Failure(new Exception("No installer found for the specified language and market."));
            }
            (string InstallerUrl, string InstallerSwitches, uint Priority) highestPriority = installersList.OrderByDescending(f => f.Priority).ElementAt(0);
            return Result<(string InstallerUrl, string InstallerSwitches)>.Success((highestPriority.InstallerUrl, highestPriority.InstallerSwitches));
        }
        catch (Exception ex)
        {
            return Result<(string InstallerUrl, string InstallerSwitches)>.Failure(ex);
        }
    }
}