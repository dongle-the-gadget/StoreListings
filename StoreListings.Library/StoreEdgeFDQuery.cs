using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

public class StoreEdgeFDQuery
{
    /// <summary>
    /// list of cards returned by the query.
    /// </summary>
    public required List<Card> Cards { get; set; }

    [SetsRequiredMembers]
    private StoreEdgeFDQuery(List<Card> cards)
    {
        Cards = cards;
    }

    public static async Task<Result<StoreEdgeFDQuery>> GetRecommendations(
        Category category,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        MediaTypeRecommendation mediaType = MediaTypeRecommendation.Apps,
        int skipItems = 0,
        int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();
        string url =
            $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/recommendations/collections/{category}?market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}&mediaType={mediaType}&pageSize={pageSize}&skipItems={skipItems}";

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            var cardsElement = json.RootElement.GetPropertySafe("Payload").GetPropertySafe("Cards");

            if (cardsElement.ValueKind == JsonValueKind.Array)
            {
                return Result<StoreEdgeFDQuery>.Success(new(Helpers.GetCards(cardsElement)));
            }

            return Result<StoreEdgeFDQuery>.Success(new StoreEdgeFDQuery(new List<Card>()));
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDQuery>.Failure(ex);
        }
    }

    public static async Task<Result<StoreEdgeFDQuery>> GetSearchProduct(
        string query,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        int skipItems = 0,
        MediaTypeSearch mediaType = MediaTypeSearch.All,
        PriceType priceType = PriceType.All,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();

        string filters = priceType == PriceType.All ? "" : $"PriceType%3d{priceType}";
        var randomString = Helpers.GenerateRandomString(21 - skipItems.ToString().Length);
        var base64string = Helpers.ToBase64Url($"o={skipItems}&s={randomString}");

        string url =
            $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/search?query={query}&market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}&mediaType={mediaType}&filters={filters}&cursor={base64string}%3d";

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using JsonDocument json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(),
                cancellationToken: cancellationToken
            );
            JsonElement payload = json.RootElement.GetPropertySafe("Payload");
            if (
                payload.ValueKind != JsonValueKind.Undefined
                && payload.TryGetProperty("SearchResults", out JsonElement searchResults)
                && searchResults.GetArrayLength() > 0
            )
            {
                return Result<StoreEdgeFDQuery>.Success(new(Helpers.GetCards(searchResults)));
            }
            return Result<StoreEdgeFDQuery>.Success(new StoreEdgeFDQuery(new List<Card>()));
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDQuery>.Failure(ex);
        }
    }

    public static async Task<Result<StoreEdgeFDQuery>> GetBundles(
        string productId,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            HttpClient client = Helpers.GetStoreHttpClient();
            string url =
                $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{productId}/BundleParts?market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}";

            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var errorDoc = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(),
                        cancellationToken: cancellationToken
                    );
                    string msg = errorDoc.RootElement.GetStringSafe("message");
                    return Result<StoreEdgeFDQuery>.Failure(
                        new Exception(!string.IsNullOrEmpty(msg) ? msg : response.ReasonPhrase)
                    );
                }
                catch
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(),
                cancellationToken: cancellationToken
            );
            JsonElement payload = json.RootElement.GetPropertySafe("Payload");
            string[] preferredBundles = ["0017", "0010"];

            foreach (var bundleId in preferredBundles)
            {
                JsonElement bundle = payload.GetPropertySafe(bundleId);
                if (bundle.ValueKind == JsonValueKind.Object)
                {
                    JsonElement products = bundle.GetPropertySafe("Products");

                    if (products.ValueKind == JsonValueKind.Array && products.GetArrayLength() >= 1)
                    {
                        return Result<StoreEdgeFDQuery>.Success(new(Helpers.GetCards(products)));
                    }
                }
            }

            return Result<StoreEdgeFDQuery>.Success(new StoreEdgeFDQuery(new List<Card>()));
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDQuery>.Failure(ex);
        }
    }
}
