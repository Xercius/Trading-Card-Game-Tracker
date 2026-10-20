using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

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
        // Numeric expansion IDs are more reliable than code-based API filtering.
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

        return await ImportRecordsAsync(allRecords, options, summary,
            $"for expansion={expansionCode}", ct);
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

        return await ImportRecordsAsync(records, options, summary,
            $"from file (set={options.SetCode ?? "unknown"})", ct);
    }

    private async Task<ImportSummary> ImportRecordsAsync(IReadOnlyList<StrapiRecord> records,
        ImportOptions options, ImportSummary summary, string description, CancellationToken ct)
    {
        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            await _db.Cards.Where(c => c.Game == "Star Wars Unlimited").LoadAsync(ct);
            var printings = await _db.CardPrintings
                .Include(p => p.Card)
                .Where(p => p.Card.Game == "Star Wars Unlimited").ToListAsync(ct);
            var identity = new SwuSharedIdentity(printings);
            var cardChanges = new SwuSharedCardChanges(summary);
            int processed = 0;
            foreach (var record in records.Take(Math.Max(0, options.Limit ?? int.MaxValue)))
            {
                ct.ThrowIfCancellationRequested();
                processed++;
                var counts = (summary.CardsCreated, summary.CardsUpdated,
                    summary.PrintingsCreated, summary.PrintingsUpdated);
                try
                {
                    var card = Upsert(record, summary, identity);
                    _db.ChangeTracker.DetectChanges();
                    var state = card is null ? EntityState.Unchanged : _db.Entry(card).State;
                    // EF rolls back failed writes to a savepoint in the import transaction.
                    await _db.SaveChangesAsync(ct);
                    if (state == EntityState.Added) cardChanges.Created(card!);
                    if (state == EntityState.Modified) cardChanges.Updated(card!);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    RestoreFailedRecord();
                    identity = new SwuSharedIdentity(_db.CardPrintings.Local
                        .Where(p => p.Card.Game == "Star Wars Unlimited"));
                    (summary.CardsCreated, summary.CardsUpdated,
                        summary.PrintingsCreated, summary.PrintingsUpdated) = counts;
                    summary.Errors++;
                    var attrs = record?.Attributes;
                    summary.Messages.Add(
                        $"Error [id={record?.Id} serialCode={attrs?.SerialCode}] {attrs?.Title}: Record could not be imported.");
                }
            }

            foreach (var card in identity.ResolveRelationships())
                cardChanges.Updated(card);
            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {processed} records {description}.");
            return summary;
        });
    }

    private void RestoreFailedRecord()
    {
        var added = _db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added).ToList();
        foreach (var printing in added.Select(e => e.Entity).OfType<CardPrinting>())
            printing.Card.Printings.Remove(printing);
        foreach (var entry in added)
            entry.State = EntityState.Detached;
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Modified).ToList())
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }
    }

    private Card? Upsert(StrapiRecord record, ImportSummary summary, SwuSharedIdentity identity)
    {
        const string game = "Star Wars Unlimited";

        var attrs = record.Attributes
            ?? throw new InvalidOperationException($"Record id={record.Id} has no attributes.");
        if (string.IsNullOrWhiteSpace(attrs.Locale))
            throw new InvalidOperationException("A locale is required.");
        if (!string.Equals(attrs.Locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            summary.Messages.Add(
                $"Skipping record id={record.Id} with locale={attrs.Locale ?? "(null)"} (expected \"en\") title={attrs.Title}.");
            return null;
        }

        if (record.Id <= 0 || string.IsNullOrWhiteSpace(attrs.Title))
            throw new InvalidOperationException("A positive source ID and title are required.");

        string name = string.IsNullOrWhiteSpace(attrs.Subtitle)
            ? attrs.Title?.Trim() ?? "Unknown"
            : $"{attrs.Title?.Trim()} \u2014 {attrs.Subtitle.Trim()}";
        string type = attrs.Type?.Data?.Attributes?.Name ?? string.Empty;
        string? text = attrs.Text;
        string set = attrs.Expansion?.Data?.Attributes?.Code?.ToUpperInvariant() ?? "UNK";
        string number = attrs.CardNumber?.ToString() ?? record.Id.ToString();
        string? serialCode = string.IsNullOrWhiteSpace(attrs.SerialCode) ? null : attrs.SerialCode.Trim();
        string? cardUid = attrs.CardUid;
        int sourceId = record.Id;
        string rarity = attrs.Rarity ?? "Unknown";

        int? variantOfSourceId = attrs.VariantOf?.Data?.Id;
        string? variantOfCardUid = attrs.VariantOf?.Data?.Attributes?.CardUid;
        int? reprintOfSourceId = attrs.ReprintOf?.Data?.Id;
        string? reprintOfCardUid = attrs.ReprintOf?.Data?.Attributes?.CardUid;

        bool isFoil = attrs.VariantTypes?.Data?.Any(vt => vt.Attributes?.Foil == true) == true;
        string style = isFoil ? "Foil" : "Standard";

        string? imageUrl = attrs.ArtFront?.Data?.Attributes?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Card?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Thumbnail?.Url
                        ?? attrs.ArtBack?.Data?.Attributes?.Url;

        // Source/serial identity takes precedence over names, which can be corrected upstream.
        var printing = identity.FindPrinting(sourceId, serialCode, set, number);
        var card = printing?.Card
                   ?? _db.Cards.Local.FirstOrDefault(x => x.Game == game && x.Name == name);

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
            updatedAt = attrs.UpdatedAt,
            variantOfSourceId,
            variantOfCardUid,
            reprintOfSourceId,
            reprintOfCardUid
        }, JsonOptions);

        // All preparation above must finish before changing tracked entities.
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
        }
        else
        {
            card.Name = name;
            card.CardType = type;
            card.Description = text;
            card.DetailsJson = cardJson;
        }

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
            if (printing.Set != set) { printing.Set = set; changed = true; }
            if (printing.Number != (serialCode ?? number)) { printing.Number = serialCode ?? number; changed = true; }
            if (printing.Rarity != rarity) { printing.Rarity = rarity; changed = true; }
            if (printing.Style != style) { printing.Style = style; changed = true; }
            if (imageUrl is not null && printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed) summary.PrintingsUpdated++;
        }
        identity.Track(printing);
        return card;
    }
}
