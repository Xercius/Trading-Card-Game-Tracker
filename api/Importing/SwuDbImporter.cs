using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace api.Importing;

/// <summary>
/// Imports Star Wars: Unlimited cards from the official card-list API at
/// https://admin.starwarsunlimited.com/api/card-list (Strapi-based endpoint).
/// The API is undocumented/internal and may change without notice.
/// </summary>
public sealed class SwuDbImporter : ISourceImporter
{
    public string Key => "swu";
    public string DisplayName => "Star Wars: Unlimited (Official API)";
    public IEnumerable<string> SupportedGames => new[] { "Star Wars Unlimited" };

    private const string BaseAddress = "https://admin.starwarsunlimited.com/api/";
    private const int PageSize = 100;

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SwuDbImporter(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _http = httpFactory.CreateClient(nameof(SwuDbImporter));
        _http.BaseAddress = new Uri(BaseAddress);
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Imports all English cards for a given expansion code (e.g. "SOR", "SOTG") from the
    /// official API, paging through all results automatically.
    /// </summary>
    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
            throw new ArgumentException("SetCode is required (expansion code, e.g. 'SOR', 'SOTG').", nameof(options));

        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun,
        };

        var expansionCode = options.SetCode!.Trim().ToUpperInvariant();
        var limit = options.Limit ?? int.MaxValue;
        int processed = 0;
        int page = 1;
        int pageCount = 1;

        var allRecords = new List<StrapiRecord>();
        while (page <= pageCount)
        {
            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["locale"] = "en";
            qs["filters[expansion][code][$eq]"] = expansionCode;
            if (options.UpdatedSince is { } since)
                qs["filters[updatedAt][$gt]"] = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            qs["pagination[page]"] = page.ToString();
            qs["pagination[pageSize]"] = PageSize.ToString();
            qs["sort[0]"] = "updatedAt:asc";
            qs["sort[1]"] = "cardNumber:asc";

            using var response = await _http.GetAsync($"card-list?{qs}", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            var page_response = await JsonSerializer.DeserializeAsync<StrapiPagedResponse>(stream, JsonOptions, ct)
                         ?? throw new InvalidOperationException("Empty response from Star Wars: Unlimited API.");

            pageCount = page_response.Meta?.Pagination?.PageCount ?? 1;
            if (page_response.Data is not null)
                allRecords.AddRange(page_response.Data);
            page++;
        }

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            foreach (var record in allRecords)
            {
                if (processed++ >= limit) break;
                try
                {
                    await UpsertAsync(record, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    var attrs = record.Attributes;
                    summary.Messages.Add(
                        $"Error [id={record.Id} serialCode={attrs?.SerialCode}] {attrs?.Title}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);

            if (!options.DryRun)
                await UpsertSyncLogAsync(expansionCode, ct);

            summary.Messages.Add(
                $"Processed {Math.Min(processed, allRecords.Count)} records for expansion={expansionCode}.");
            return summary;
        });
    }

    /// <summary>
    /// Imports from a Strapi-format JSON file (same structure as the API response:
    /// <c>{ "data": [ ... ], "meta": { ... } }</c>). A plain JSON array is also accepted
    /// for backwards compatibility.
    /// </summary>
    public async Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun,
        };

        // Accept either a full Strapi page response or a bare array of records.
        List<StrapiRecord> records;
        using var doc = await JsonDocument.ParseAsync(file, cancellationToken: ct);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            records = JsonSerializer.Deserialize<List<StrapiRecord>>(doc.RootElement.GetRawText(), JsonOptions)
                      ?? throw new InvalidOperationException("Empty array in file.");
        }
        else
        {
            var paged = JsonSerializer.Deserialize<StrapiPagedResponse>(doc.RootElement.GetRawText(), JsonOptions)
                        ?? throw new InvalidOperationException("Empty Strapi response in file.");
            records = paged.Data ?? throw new InvalidOperationException("Missing 'data' array in file.");
        }

        var limit = options.Limit ?? int.MaxValue;
        int processed = 0;

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            foreach (var record in records)
            {
                if (processed++ >= limit) break;
                try
                {
                    await UpsertAsync(record, summary, ct);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    var attrs = record.Attributes;
                    summary.Messages.Add(
                        $"Error [id={record.Id} serialCode={attrs?.SerialCode}] {attrs?.Title}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);

            if (!options.DryRun && options.SetCode is not null)
                await UpsertSyncLogAsync(options.SetCode.Trim().ToUpperInvariant(), ct);

            summary.Messages.Add(
                $"Processed {Math.Min(processed, records.Count)} records from file (set={options.SetCode ?? "unknown"}).");
            return summary;
        });
    }

    private async Task UpsertAsync(StrapiRecord record, ImportSummary summary, CancellationToken ct)
    {
        const string game = "Star Wars Unlimited";

        var attrs = record.Attributes
            ?? throw new InvalidOperationException($"Record id={record.Id} has no attributes.");

        if (!string.Equals(attrs.Locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            summary.Messages.Add(
                $"Skipping record id={record.Id} with locale={attrs.Locale ?? "(null)"} (expected \"en\") title={attrs.Title}.");
            return;
        }

        string name = string.IsNullOrWhiteSpace(attrs.Subtitle)
            ? attrs.Title?.Trim() ?? "Unknown"
            : $"{attrs.Title?.Trim()} \u2014 {attrs.Subtitle.Trim()}";
        string type = attrs.Type?.Data?.Attributes?.Name ?? string.Empty;
        string? text = attrs.Text;
        string set = attrs.Expansion?.Data?.Attributes?.Code?.ToUpperInvariant() ?? "UNK";
        string number = attrs.CardNumber?.ToString() ?? record.Id.ToString();
        string? serialCode = attrs.SerialCode;
        string? cardUid = attrs.CardUid;
        int sourceId = record.Id;
        string rarity = attrs.Rarity ?? "Unknown";

        // variantOf / reprintOf relationship metadata from the API.
        int? variantOfSourceId = attrs.VariantOf?.Data?.Id;
        string? variantOfCardUid = attrs.VariantOf?.Data?.Attributes?.CardUid;
        string? variantOfTitle = attrs.VariantOf?.Data?.Attributes?.Title?.Trim();
        int? reprintOfSourceId = attrs.ReprintOf?.Data?.Id;
        string? reprintOfCardUid = attrs.ReprintOf?.Data?.Attributes?.CardUid;

        // Foil detection: check if any variantType has foil=true.
        bool isFoil = attrs.VariantTypes?.Data?.Any(vt => vt.Attributes?.Foil == true) == true;
        string style = isFoil ? "Foil" : "Standard";

        // Image: prefer artFront original URL, then card-sized format, then artBack.
        string? imageUrl = attrs.ArtFront?.Data?.Attributes?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Card?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Thumbnail?.Url
                        ?? attrs.ArtBack?.Data?.Attributes?.Url;

        // The printing key: use serialCode when present (strongest identifier), else set+number.
        string printingKey = serialCode ?? $"{set}-{number}";

        var card = _db.Cards.Local.FirstOrDefault(x => x.Game == game && x.Name == name)
                   ?? await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == name, ct);

        var cardJson = JsonSerializer.Serialize(new
        {
            sourceId,
            cardUid,
            serialCode,
            subtitle = attrs.Subtitle,
            type,
            traits = attrs.Traits,
            keywords = attrs.Keywords,
            aspects = attrs.Aspects,
            arena = attrs.Arena,
            power = attrs.Power,
            health = attrs.Health,
            cost = attrs.Cost,
            text,
            artist = attrs.Artist,
            variantOfSourceId,
            variantOfCardUid,
            reprintOfSourceId,
            reprintOfCardUid
        }, JsonOptions);

        if (card is null)
        {
            card = new Card
            {
                Game = game,
                Name = name,
                CardType = type,
                Description = text,
                DetailsJson = cardJson
            };
            _db.Cards.Add(card);
            summary.CardsCreated++;
        }
        else
        {
            bool changed = false;
            if (card.CardType != type) { card.CardType = type; changed = true; }
            if (card.Description != text) { card.Description = text; changed = true; }
            if (card.DetailsJson != cardJson) { card.DetailsJson = cardJson; changed = true; }
            if (changed) summary.CardsUpdated++;
        }

        // Resolve BaseCardId from the variantOf relationship.
        // Try to find the base card by title within the same game (using local cache first).
        if (variantOfTitle is not null)
        {
            var baseCard = _db.Cards.Local.FirstOrDefault(x => x.Game == game && x.Name == variantOfTitle)
                           ?? await _db.Cards.FirstOrDefaultAsync(x => x.Game == game && x.Name == variantOfTitle, ct);
            if (baseCard is not null && card.BaseCardId != baseCard.Id)
                card.BaseCardId = baseCard.Id;
        }

        // Find existing printing by serialCode (if available) or by set+number within this game.
        var printing = serialCode is not null
            ? await _db.CardPrintings
                .Where(p => p.Number == serialCode)
                .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, c => c.Id, (p, _) => p)
                .FirstOrDefaultAsync(ct)
            : await _db.CardPrintings
                .Where(p => p.Set == set && p.Number == number)
                .Join(_db.Cards.Where(x => x.Game == game), p => p.CardId, c => c.Id, (p, _) => p)
                .FirstOrDefaultAsync(ct);

        var printingJson = JsonSerializer.Serialize(new
        {
            sourceId,
            cardUid,
            serialCode,
            set,
            number,
            rarity,
            style,
            aspects = attrs.Aspects,
            cost = attrs.Cost,
            createdAt = attrs.CreatedAt,
            updatedAt = attrs.UpdatedAt
        }, JsonOptions);

        if (printing is null)
        {
            printing = new CardPrinting
            {
                Card = card,
                Set = set,
                // Store serialCode as Number when present so future lookups match correctly.
                Number = serialCode ?? number,
                Rarity = rarity,
                Style = style,
                ImageUrl = imageUrl,
                DetailsJson = printingJson
            };
            _db.CardPrintings.Add(printing);
            summary.PrintingsCreated++;
        }
        else
        {
            bool changed = false;
            if (printing.CardId != card.Id) { printing.CardId = card.Id; changed = true; }
            if (printing.Set != set) { printing.Set = set; changed = true; }
            if (printing.Rarity != rarity) { printing.Rarity = rarity; changed = true; }
            if (printing.Style != style) { printing.Style = style; changed = true; }
            if (imageUrl is not null && printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sync log helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or updates the <see cref="ImportSyncLog"/> row for this importer and set,
    /// recording <see cref="DateTimeOffset.UtcNow"/> as <c>LastSyncedAt</c>.  Called at the
    /// end of every successful non-dry-run import so the next run can pass the stored
    /// timestamp as <c>ImportOptions.UpdatedSince</c> to perform an incremental sync.
    /// </summary>
    private async Task UpsertSyncLogAsync(string setCode, CancellationToken ct)
    {
        var entry = await _db.ImportSyncLogs
            .FirstOrDefaultAsync(s => s.Source == Key && s.SetCode == setCode, ct);

        if (entry is null)
        {
            _db.ImportSyncLogs.Add(new api.Models.ImportSyncLog
            {
                Source = Key,
                SetCode = setCode,
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            entry.LastSyncedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Strapi response models
    // ──────────────────────────────────────────────────────────────────────────

    private sealed record StrapiPagedResponse(
        [property: JsonPropertyName("data")] List<StrapiRecord>? Data,
        [property: JsonPropertyName("meta")] StrapiMeta? Meta);

    private sealed record StrapiMeta(
        [property: JsonPropertyName("pagination")] StrapiPagination? Pagination);

    private sealed record StrapiPagination(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize,
        [property: JsonPropertyName("pageCount")] int PageCount,
        [property: JsonPropertyName("total")] int Total);

    private sealed record StrapiRecord(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("attributes")] SwuCardAttributes? Attributes);

    private sealed record SwuCardAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("subtitle")] string? Subtitle,
        [property: JsonPropertyName("cardUid")] string? CardUid,
        [property: JsonPropertyName("serialCode")] string? SerialCode,
        [property: JsonPropertyName("locale")] string? Locale,
        [property: JsonPropertyName("cardNumber")] int? CardNumber,
        [property: JsonPropertyName("rarity")] string? Rarity,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("artist")] string? Artist,
        [property: JsonPropertyName("cost")] int? Cost,
        [property: JsonPropertyName("power")] int? Power,
        [property: JsonPropertyName("health")] int? Health,
        [property: JsonPropertyName("arena")] string? Arena,
        [property: JsonPropertyName("aspects")] string[]? Aspects,
        [property: JsonPropertyName("traits")] string[]? Traits,
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

    private sealed record StrapiRelation<T>(
        [property: JsonPropertyName("data")] StrapiRelationData<T>? Data);

    private sealed record StrapiRelationList<T>(
        [property: JsonPropertyName("data")] List<StrapiRelationData<T>>? Data);

    private sealed record StrapiRelationData<T>(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("attributes")] T? Attributes);

    private sealed record SwuTypeAttributes(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("value")] string? Value);

    private sealed record SwuExpansionAttributes(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("code")] string? Code);

    private sealed record SwuVariantTypeAttributes(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("variantId")] string? VariantId,
        [property: JsonPropertyName("foil")] bool? Foil);

    private sealed record SwuVariantRefAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("cardUid")] string? CardUid);

    private sealed record SwuImageAttributes(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("formats")] SwuImageFormats? Formats);

    private sealed record SwuImageFormats(
        [property: JsonPropertyName("card")] SwuImageFormat? Card,
        [property: JsonPropertyName("thumbnail")] SwuImageFormat? Thumbnail);

    private sealed record SwuImageFormat(
        [property: JsonPropertyName("url")] string? Url);
}
