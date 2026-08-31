using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

public class StoreEdgeFDSuggestions
{
    public List<string> Suggestions
    {
        get; set;
    }
    public required List<Card> Cards
    {
        get; set;
    }

    [SetsRequiredMembers]
    private StoreEdgeFDSuggestions(List<Card> cards, List<string> suggestions)
    {
        Cards = cards;
        Suggestions = suggestions;
    }

    public static async Task<Result<StoreEdgeFDSuggestions>> GetSearchSuggestion(
        string query,
        DeviceFamily deviceFamily,
        Market market,
        Lang language,
        CancellationToken cancellationToken = default
    )
    {
        HttpClient client = Helpers.GetStoreHttpClient();
        string url =
            $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/autosuggest?prefix={query}&market={market}&locale={language}-{market}&deviceFamily=Windows.{deviceFamily}";

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode(); // Standard check

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument jsonDoc = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken
            );

            if (!jsonDoc.RootElement.TryGetProperty("Payload", out JsonElement payload))
            {
                return Result<StoreEdgeFDSuggestions>.Failure(
                    new Exception("Response missing 'Payload'")
                );
            }

            List<string> suggestions = payload
                .GetArraySafe("SearchSuggestions")
                .EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList()!;

            List<Card> cards = Helpers.GetCards(payload.GetArraySafe("AssetSuggestions"));

            return Result<StoreEdgeFDSuggestions>.Success(new(cards, suggestions));
        }
        catch (Exception ex)
        {
            return Result<StoreEdgeFDSuggestions>.Failure(ex);
        }
    }
}
