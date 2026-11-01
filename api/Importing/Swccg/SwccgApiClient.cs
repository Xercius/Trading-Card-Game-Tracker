using System.Text.Json;

namespace api.Importing.Swccg;

/// <summary>
/// HTTP client implementation fetching official SWCCG catalog files from swccg-card-json raw endpoints.
/// </summary>
public sealed class SwccgApiClient : ISwccgApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SwccgApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
    {
        using var stream = await _http.GetStreamAsync("sets.json", ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.Deserialize<List<SwccgSet>>(JsonOptions) ?? [];
        }
        if (doc.RootElement.TryGetProperty("sets", out var setsElement))
        {
            return setsElement.Deserialize<List<SwccgSet>>(JsonOptions) ?? [];
        }
        return [];
    }

    public async Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
    {
        return await FetchCardsAsync("Dark.json", ct);
    }

    public async Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
    {
        return await FetchCardsAsync("Light.json", ct);
    }

    private async Task<List<SwccgCardRecord>> FetchCardsAsync(string fileName, CancellationToken ct)
    {
        using var stream = await _http.GetStreamAsync(fileName, ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return doc.RootElement.Deserialize<List<SwccgCardRecord>>(JsonOptions) ?? [];
        }
        if (doc.RootElement.TryGetProperty("cards", out var cardsElement))
        {
            return cardsElement.Deserialize<List<SwccgCardRecord>>(JsonOptions) ?? [];
        }
        return [];
    }

    public async Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
    {
        var darkTask = GetDarkCardsAsync(ct);
        var lightTask = GetLightCardsAsync(ct);
        await Task.WhenAll(darkTask, lightTask);

        var all = new List<SwccgCardRecord>(darkTask.Result.Count + lightTask.Result.Count);
        all.AddRange(darkTask.Result);
        all.AddRange(lightTask.Result);
        return all;
    }
}
