using System.Net;
using System.Text.Json;

namespace api.Importing.Mtg;

/// <summary>
/// Rate-limited, SSRF-safe HTTP client for the Scryfall REST API.
/// Enforces Scryfall's User-Agent, Accept, &lt;10 req/s standard rate limit, &lt;2 req/s batch rate limit, and 429 backoff.
/// </summary>
public sealed class ScryfallApiClient : IMtgApiClient
{
    private const int MaxPages = 200;
    private const int MaxRetries = 3;
    private static readonly TimeSpan StandardMinInterval = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan BatchMinInterval = TimeSpan.FromMilliseconds(550);

    private static readonly HashSet<string> AllowedApiHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.scryfall.com"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<ScryfallApiClient> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _standardGate = new(1, 1);
    private readonly SemaphoreSlim _batchGate = new(1, 1);
    private DateTimeOffset _lastStandardRequestAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBatchRequestAt = DateTimeOffset.MinValue;

    public ScryfallApiClient(
        HttpClient http,
        ILogger<ScryfallApiClient> logger,
        TimeProvider? timeProvider = null)
    {
        _http = http;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.scryfall.com/");
        }

        _http.Timeout = TimeSpan.FromMinutes(5);

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("TCGTracker/1.0");
        }

        if (!_http.DefaultRequestHeaders.Accept.Any(a => string.Equals(a.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
        {
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScryfallSet>> GetSetsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching MTG sets list from Scryfall...");
        var uri = ValidateApiUri("sets");
        var bytes = await SendRateLimitedAsync(uri, isBatchEndpoint: false, ct);
        var response = JsonSerializer.Deserialize<ScryfallListResponse<ScryfallSet>>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("Empty sets response from Scryfall.");
        return response.Data;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScryfallCard>> GetSetCardsAsync(string setCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setCode);
        var normalizedCode = setCode.Trim().ToLowerInvariant();
        var cards = new List<ScryfallCard>();
        var nextUrl = $"cards/search?q={Uri.EscapeDataString($"set:{normalizedCode} lang:en")}&unique=prints&order=set";

        var pageCount = 0;
        while (!string.IsNullOrWhiteSpace(nextUrl) && pageCount++ < MaxPages)
        {
            var uri = ValidateApiUri(nextUrl);
            var bytes = await SendRateLimitedAsync(uri, isBatchEndpoint: false, ct, allowNotFound: true);
            if (bytes is null)
            {
                break;
            }

            var page = JsonSerializer.Deserialize<ScryfallListResponse<ScryfallCard>>(bytes, JsonOptions)
                ?? throw new InvalidOperationException("Empty card search response from Scryfall.");

            cards.AddRange(page.Data);
            nextUrl = page.HasMore ? page.NextPage : null;
        }

        return cards;
    }

    private Uri ValidateApiUri(string relativeOrAbsolute)
    {
        if (relativeOrAbsolute.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!ImportHttpGuard.TryValidateAllowedHttpsUri(relativeOrAbsolute, AllowedApiHosts, out var validated) || validated is null)
            {
                throw new InvalidOperationException("Rejected Scryfall URL outside allow-listed host.");
            }
            return validated;
        }

        if (relativeOrAbsolute.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rejected network-path Scryfall URL.");
        }

        var candidate = new Uri(_http.BaseAddress!, relativeOrAbsolute);
        if (!ImportHttpGuard.TryValidateAllowedHttpsUri(candidate.AbsoluteUri, AllowedApiHosts, out var resolved) || resolved is null)
        {
            throw new InvalidOperationException("Rejected Scryfall URL outside allow-listed host.");
        }

        return resolved;
    }

    private async Task<byte[]?> SendRateLimitedAsync(
        Uri requestUri,
        bool isBatchEndpoint,
        CancellationToken ct,
        bool allowNotFound = false)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            await WaitForRateLimitSlotAsync(isBatchEndpoint, ct);

            using var response = await _http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, ct);
            ImportHttpGuard.EnsureNotRedirect(response);

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if ((response.StatusCode == HttpStatusCode.TooManyRequests ||
                 response.StatusCode == HttpStatusCode.ServiceUnavailable) &&
                attempt < MaxRetries)
            {
                var delay = ComputeBackoffDelay(response, attempt);
                _logger.LogWarning(
                    "Scryfall returned {StatusCode} for {Uri}. Backing off for {DelayMs}ms (attempt {Attempt}/{MaxRetries}).",
                    (int)response.StatusCode,
                    requestUri,
                    delay.TotalMilliseconds,
                    attempt + 1,
                    MaxRetries);
                await Task.Delay(delay, _timeProvider, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is { } contentLength &&
                contentLength > ImportHttpGuard.DefaultMaxJsonBytes)
            {
                throw new InvalidOperationException(
                    $"Remote response exceeded maximum allowed size of {ImportHttpGuard.DefaultMaxJsonBytes} bytes.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await ImportHttpGuard.ReadStreamWithLimitAsync(stream, ImportHttpGuard.DefaultMaxJsonBytes, ct);
        }

        throw new HttpRequestException($"Scryfall request failed after {MaxRetries} retries: {requestUri}");
    }

    private async Task WaitForRateLimitSlotAsync(bool isBatchEndpoint, CancellationToken ct)
    {
        var gate = isBatchEndpoint ? _batchGate : _standardGate;
        var minInterval = isBatchEndpoint ? BatchMinInterval : StandardMinInterval;

        await gate.WaitAsync(ct);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var last = isBatchEndpoint ? _lastBatchRequestAt : _lastStandardRequestAt;
            var elapsed = now - last;

            if (last != DateTimeOffset.MinValue && elapsed < minInterval)
            {
                await Task.Delay(minInterval - elapsed, _timeProvider, ct);
            }

            var updatedNow = _timeProvider.GetUtcNow();
            if (isBatchEndpoint)
            {
                _lastBatchRequestAt = updatedNow;
            }
            else
            {
                _lastStandardRequestAt = updatedNow;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static TimeSpan ComputeBackoffDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta <= TimeSpan.FromSeconds(30) ? delta : TimeSpan.FromSeconds(30);
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                return wait <= TimeSpan.FromSeconds(30) ? wait : TimeSpan.FromSeconds(30);
            }
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}
