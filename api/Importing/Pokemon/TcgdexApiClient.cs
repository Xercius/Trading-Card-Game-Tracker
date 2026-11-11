using System.Net;
using System.Text.Json;

namespace api.Importing.Pokemon;

/// <summary>
/// HTTP client implementation communicating with the public TCGdex REST API.
/// </summary>
public sealed class TcgdexApiClient : IPokemonApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<TcgdexApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TcgdexApiClient(HttpClient http, ILogger<TcgdexApiClient> logger)
    {
        _http = http;
        _logger = logger;

        if (_http.BaseAddress == null)
        {
            _http.BaseAddress = new Uri("https://api.tcgdex.net/v2/en/");
        }
        _http.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<IReadOnlyList<TcgdexSetResume>> GetSetsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching Pokemon sets list from TCGdex...");
        var sets = await _http.GetFromJsonAsync<List<TcgdexSetResume>>("sets", JsonOptions, ct);
        return sets ?? (IReadOnlyList<TcgdexSetResume>)Array.Empty<TcgdexSetResume>();
    }

    public async Task<TcgdexSetDetail?> GetSetAsync(string setId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        var url = $"sets/{Uri.EscapeDataString(setId.Trim().ToLowerInvariant())}";
        using var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TcgdexSetDetail>(JsonOptions, ct);
    }

    public async Task<TcgdexCard?> GetCardAsync(string cardId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var url = $"cards/{Uri.EscapeDataString(cardId.Trim())}";
        using var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TcgdexCard>(JsonOptions, ct);
    }

    public async Task<IReadOnlyList<TcgdexSerieResume>> GetSeriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching Pokemon series list from TCGdex...");
        var series = await _http.GetFromJsonAsync<List<TcgdexSerieResume>>("series", JsonOptions, ct);
        return series ?? (IReadOnlyList<TcgdexSerieResume>)Array.Empty<TcgdexSerieResume>();
    }
}
