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
        var bytes = await ImportHttpGuard.GetBytesWithLimitAsync(
            _http,
            new Uri(_http.BaseAddress!, "sets"),
            ImportHttpGuard.DefaultMaxJsonBytes,
            ct);
        var sets = JsonSerializer.Deserialize<List<TcgdexSetResume>>(bytes, JsonOptions);
        return sets ?? (IReadOnlyList<TcgdexSetResume>)Array.Empty<TcgdexSetResume>();
    }

    public async Task<TcgdexSetDetail?> GetSetAsync(string setId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        var url = $"sets/{Uri.EscapeDataString(setId.Trim().ToLowerInvariant())}";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        ImportHttpGuard.EnsureNotRedirect(resp);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var bytes = await ImportHttpGuard.ReadStreamWithLimitAsync(stream, ImportHttpGuard.DefaultMaxJsonBytes, ct);
        return JsonSerializer.Deserialize<TcgdexSetDetail>(bytes, JsonOptions);
    }

    public async Task<TcgdexCard?> GetCardAsync(string cardId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var url = $"cards/{Uri.EscapeDataString(cardId.Trim())}";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        ImportHttpGuard.EnsureNotRedirect(resp);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var bytes = await ImportHttpGuard.ReadStreamWithLimitAsync(stream, ImportHttpGuard.DefaultMaxJsonBytes, ct);
        return JsonSerializer.Deserialize<TcgdexCard>(bytes, JsonOptions);
    }

    public async Task<IReadOnlyList<TcgdexSerieResume>> GetSeriesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching Pokemon series list from TCGdex...");
        var bytes = await ImportHttpGuard.GetBytesWithLimitAsync(
            _http,
            new Uri(_http.BaseAddress!, "series"),
            ImportHttpGuard.DefaultMaxJsonBytes,
            ct);
        var series = JsonSerializer.Deserialize<List<TcgdexSerieResume>>(bytes, JsonOptions);
        return series ?? (IReadOnlyList<TcgdexSerieResume>)Array.Empty<TcgdexSerieResume>();
    }
}
