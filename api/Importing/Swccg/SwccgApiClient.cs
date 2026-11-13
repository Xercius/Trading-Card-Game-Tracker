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
        if (_http.Timeout == Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromMinutes(5);
        }
    }

    /// <summary>Fetches all SWCCG set metadata from <c>sets.json</c>.</summary>
    public async Task<List<SwccgSet>> GetSetsAsync(CancellationToken ct = default)
    {
        var bytes = await ReadBoundedAsync("sets.json", ct);
        using var doc = JsonDocument.Parse(bytes);
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

    /// <summary>Fetches all Dark Side SWCCG card records from <c>Dark.json</c>.</summary>
    public Task<List<SwccgCardRecord>> GetDarkCardsAsync(CancellationToken ct = default)
        => FetchCardsAsync("Dark.json", ct);

    /// <summary>Fetches all Light Side SWCCG card records from <c>Light.json</c>.</summary>
    public Task<List<SwccgCardRecord>> GetLightCardsAsync(CancellationToken ct = default)
        => FetchCardsAsync("Light.json", ct);

    private async Task<List<SwccgCardRecord>> FetchCardsAsync(string fileName, CancellationToken ct)
    {
        var bytes = await ReadBoundedAsync(fileName, ct);
        using var doc = JsonDocument.Parse(bytes);
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

    private async Task<byte[]> ReadBoundedAsync(string relativePath, CancellationToken ct)
    {
        using var response = await _http.GetAsync(relativePath, HttpCompletionOption.ResponseHeadersRead, ct);
        ImportHttpGuard.EnsureNotRedirect(response);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } len && len > ImportHttpGuard.DefaultMaxJsonBytes)
        {
            throw new InvalidOperationException($"Response for {relativePath} exceeded {ImportHttpGuard.DefaultMaxJsonBytes} bytes.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await ImportHttpGuard.ReadStreamWithLimitAsync(stream, ImportHttpGuard.DefaultMaxJsonBytes, ct);
    }

    /// <summary>Fetches both Dark and Light side SWCCG cards in parallel.</summary>
    public async Task<List<SwccgCardRecord>> GetAllCardsAsync(CancellationToken ct = default)
    {
        var darkTask = GetDarkCardsAsync(ct);
        var lightTask = GetLightCardsAsync(ct);
        await Task.WhenAll(darkTask, lightTask);

        var dark = await darkTask;
        var light = await lightTask;
        var all = new List<SwccgCardRecord>(dark.Count + light.Count);
        all.AddRange(dark);
        all.AddRange(light);
        return all;
    }
}
