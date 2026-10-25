using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace api.Importing;

// ──────────────────────────────────────────────────────────────────────────────
// Public contract
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Filter parameters forwarded to the SWU card-list endpoint.</summary>
internal sealed record SWUCardFilter(
    string? ExpansionCode = null,
    int? ExpansionId = null,
    DateTimeOffset? UpdatedSince = null);

/// <summary>HTTP client abstraction for the Star Wars: Unlimited card-data API.</summary>
internal interface ISWUApiClient
{
    /// <summary>
    /// Fetches all cards that match <paramref name="filter"/>, paging through every
    /// page of the Strapi endpoint automatically.
    /// </summary>
    /// <param name="filter">Expansion and date-range constraints.</param>
    /// <param name="ct">Propagated cancellation token.</param>
    /// <returns>All matching <see cref="StrapiRecord"/> objects in ascending <c>updatedAt</c> order.</returns>
    Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(SWUCardFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Resolves an expansion code (e.g. <c>"SOR"</c>) to its internal Strapi numeric ID.
    /// Returns <c>null</c> if the lookup request fails for any reason, allowing callers to
    /// fall back to code-based filtering.
    /// </summary>
    Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default);
}

// ──────────────────────────────────────────────────────────────────────────────
// Implementation
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Dedicated HTTP client for the Star Wars: Unlimited card-data API
/// (<c>https://admin.starwarsunlimited.com/api/</c>).
/// <para>
/// Handles authentication (currently none required), transparent pagination
/// across all Strapi pages, and automatic retry on HTTP 429 responses using
/// exponential back-off.
/// </para>
/// </summary>
internal sealed class SWUApiClient : ISWUApiClient
{
    private const int PageSize = 100;
    private const int MaxRetries = 3;

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Initialises the client using the typed <see cref="HttpClient"/> injected by DI.</summary>
    public SWUApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StrapiRecord>> GetAllCardsAsync(
        SWUCardFilter filter,
        CancellationToken ct = default)
    {
        var all = new List<StrapiRecord>();
        var sourceIds = new HashSet<int>();
        int page = 1;
        int pageCount = 1;
        int? total = null;

        while (page <= pageCount)
        {
            var qs = BuildCardListQuery(filter, page);
            var paged = await FetchPageAsync($"card-list?{qs}", ct);

            var pagination = paged.Meta?.Pagination;
            if (paged.Data is null || pagination is null
                || pagination.Page != page || pagination.PageSize <= 0
                || pagination.PageCount < 0 || pagination.Total < 0
                || (pagination.PageCount == 0 && (pagination.Total != 0 || paged.Data.Count != 0))
                || (pagination.PageCount > 0 && pagination.PageCount < page)
                || (page > 1 && pagination.PageCount != pageCount)
                || (total is not null && pagination.Total != total)
                || paged.Data.Count > pagination.PageSize
                || (pagination.Total > 0 && paged.Data.Count == 0))
                throw new InvalidOperationException("Invalid pagination response from Star Wars: Unlimited API.");

            pageCount = pagination.PageCount;
            total = pagination.Total;
            if (paged.Data.Any(record => record is { Id: > 0 } && !sourceIds.Add(record.Id)))
                throw new InvalidOperationException("Repeated card identity in Star Wars: Unlimited API pagination.");
            all.AddRange(paged.Data);

            page++;
        }

        if (all.Count != total)
            throw new InvalidOperationException("Incomplete pagination response from Star Wars: Unlimited API.");

        return all;
    }

    /// <inheritdoc/>
    public async Task<int?> TryResolveExpansionIdAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["locale"] = "en";
            qs["filters[expansion][code][$eq]"] = code;
            qs["pagination[pageSize]"] = "1";

            var paged = await FetchPageAsync($"card-list?{qs}", ct);
            return paged.Data?.FirstOrDefault()?.Attributes?.Expansion?.Data?.Id;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Builds the query string for one page of the card-list endpoint.</summary>
    private static string BuildCardListQuery(SWUCardFilter filter, int page)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["locale"] = "en";

        if (filter.ExpansionId is { } id)
            qs["filters[expansion][id][$eq]"] = id.ToString();
        else if (filter.ExpansionCode is { } code)
            qs["filters[expansion][code][$eq]"] = code;

        if (filter.UpdatedSince is { } since)
            qs["filters[updatedAt][$gt]"] = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        qs["pagination[page]"] = page.ToString();
        qs["pagination[pageSize]"] = PageSize.ToString();
        qs["sort[0]"] = "updatedAt:asc";
        qs["sort[1]"] = "cardNumber:asc";
        qs["sort[2]"] = "id:asc";

        return qs.ToString()!;
    }

    /// <summary>
    /// Sends a GET request to <paramref name="relativeUrl"/> and deserialises the Strapi
    /// response, retrying on HTTP 429 with exponential back-off.
    /// Throws <see cref="HttpRequestException"/> for all other non-success responses.
    /// </summary>
    private async Task<StrapiPagedResponse> FetchPageAsync(string relativeUrl, CancellationToken ct)
    {
        int attempt = 0;
        while (true)
        {
            using var response = await _http.GetAsync(relativeUrl, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRetries)
            {
                // Respect Retry-After header if present; otherwise use exponential back-off.
                var delay = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(delay, ct);
                attempt++;
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<StrapiPagedResponse>(stream, JsonOptions, ct)
                   ?? throw new InvalidOperationException("Empty response from Star Wars: Unlimited API.");
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Strapi response models  (internal; shared with SwuDbImporter)
// ──────────────────────────────────────────────────────────────────────────────

internal sealed record StrapiPagedResponse(
    [property: JsonPropertyName("data")] List<StrapiRecord>? Data,
    [property: JsonPropertyName("meta")] StrapiMeta? Meta);

internal sealed record StrapiMeta(
    [property: JsonPropertyName("pagination")] StrapiPagination? Pagination);

internal sealed record StrapiPagination(
    [property: JsonRequired]
    [property: JsonPropertyName("page")] int Page,
    [property: JsonRequired]
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonRequired]
    [property: JsonPropertyName("pageCount")] int PageCount,
    [property: JsonRequired]
    [property: JsonPropertyName("total")] int Total);

internal sealed record StrapiRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("attributes")] SwuCardAttributes? Attributes);

internal sealed record SwuCardAttributes(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("cardUid")] string? CardUid,
    [property: JsonPropertyName("serialCode")] string? SerialCode,
    [property: JsonPropertyName("locale")] string? Locale,
    [property: JsonPropertyName("cardNumber")] int? CardNumber,
    [property: JsonConverter(typeof(SwuNameConverter))]
    [property: JsonPropertyName("rarity")] string? Rarity,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("artist")] string? Artist,
    [property: JsonPropertyName("cost")] int? Cost,
    [property: JsonPropertyName("power")] int? Power,
    [property: JsonPropertyName("health")] int? Health,
    [property: JsonPropertyName("arena")] string? Arena,
    [property: JsonConverter(typeof(SwuNameListConverter))]
    [property: JsonPropertyName("aspects")] string[]? Aspects,
    [property: JsonConverter(typeof(SwuNameListConverter))]
    [property: JsonPropertyName("traits")] string[]? Traits,
    [property: JsonConverter(typeof(SwuNameListConverter))]
    [property: JsonPropertyName("keywords")] string[]? Keywords,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("type")] StrapiRelation<SwuTypeAttributes>? Type,
    [property: JsonPropertyName("expansion")] StrapiRelation<SwuExpansionAttributes>? Expansion,
    [property: JsonPropertyName("variantTypes")] StrapiRelationList<SwuVariantTypeAttributes>? VariantTypes,
    [property: JsonPropertyName("variantOf")] StrapiRelation<SwuVariantRefAttributes>? VariantOf,
    [property: JsonPropertyName("reprintOf")] StrapiRelation<SwuVariantRefAttributes>? ReprintOf,
    [property: JsonPropertyName("artFront")] StrapiRelation<SwuImageAttributes>? ArtFront,
    [property: JsonPropertyName("artBack")] StrapiRelation<SwuImageAttributes>? ArtBack);

internal sealed record StrapiRelation<T>(
    [property: JsonPropertyName("data")] StrapiRelationData<T>? Data);

internal sealed record StrapiRelationList<T>(
    [property: JsonPropertyName("data")] List<StrapiRelationData<T>>? Data);

internal sealed record StrapiRelationData<T>(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("attributes")] T? Attributes);

internal sealed record SwuNameAttributes(
    [property: JsonPropertyName("name")] string? Name);

/// <summary>Normalizes a live Strapi relationship or legacy string to its name.</summary>
internal sealed class SwuNameConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();

        var relationship = JsonSerializer.Deserialize<StrapiRelation<SwuNameAttributes>>(ref reader, options);
        return relationship?.Data?.Attributes?.Name;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

/// <summary>Normalizes live Strapi relationships and legacy file arrays to names.</summary>
internal sealed class SwuNameListConverter : JsonConverter<string[]>
{
    /// <inheritdoc/>
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize<string[]>(ref reader, options);

        var relationship = JsonSerializer.Deserialize<StrapiRelationList<SwuNameAttributes>>(ref reader, options);
        return relationship?.Data?.Select(item => item?.Attributes?.Name
            ?? throw new JsonException("SWU relationship entry is missing its name.")).ToArray();
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

internal sealed record SwuTypeAttributes(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("value")] string? Value);

internal sealed record SwuExpansionAttributes(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("code")] string? Code);

internal sealed record SwuVariantTypeAttributes(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("variantId")] string? VariantId,
    [property: JsonPropertyName("foil")] bool? Foil);

internal sealed record SwuVariantRefAttributes(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("cardUid")] string? CardUid);

internal sealed record SwuImageAttributes(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("formats")] SwuImageFormats? Formats);

internal sealed record SwuImageFormats(
    [property: JsonPropertyName("card")] SwuImageFormat? Card,
    [property: JsonPropertyName("thumbnail")] SwuImageFormat? Thumbnail);

internal sealed record SwuImageFormat(
    [property: JsonPropertyName("url")] string? Url);
