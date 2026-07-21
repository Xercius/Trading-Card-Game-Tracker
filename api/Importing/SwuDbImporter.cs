using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

    private readonly AppDbContext _db;
    private readonly ISWUApiClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal SwuDbImporter(AppDbContext db, ISWUApiClient client)
    {
        _db = db;
        _client = client;
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

        // Discovery step: resolve the expansion code to its internal numeric Strapi ID.
        // Filtering by ID is more reliable than filtering by code (see docs/SWUAPI_DOCUMENTATION.txt §5).
        int? expansionId = await _client.TryResolveExpansionIdAsync(expansionCode, ct);
        if (expansionId is null)
        {
            summary.Messages.Add(
                $"Warning: could not resolve numeric expansion ID for '{expansionCode}'; falling back to code-based filter.");
        }

        var filter = new SWUCardFilter(
            ExpansionCode: expansionId is null ? expansionCode : null,
            ExpansionId: expansionId,
            UpdatedSince: options.UpdatedSince);

        var allRecords = await _client.GetAllCardsAsync(filter, ct);

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
}
