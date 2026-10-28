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
    private readonly ILogger<SwuDbImporter> _logger;
    private readonly TimeProvider _timeProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal SwuDbImporter(AppDbContext db, ISWUApiClient client, ILogger<SwuDbImporter> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _client = client;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Imports all English cards from the official API, paging through all results.
    /// Omit SetCode for the full catalog, or supply an expansion code for a single set.
    /// </summary>
    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun,
        };

        if (string.IsNullOrWhiteSpace(options.SetCode))
        {
            var records = await _client.GetAllCardsAsync(
                new SWUCardFilter(UpdatedSince: options.UpdatedSince), ct);
            return await ImportRecordsAsync(records, options, summary, "across all expansions", ct, requireExpansion: true);
        }

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
        ImportOptions options, ImportSummary summary, string description, CancellationToken ct,
        bool requireExpansion = false)
    {
        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            var catalog = new SwuCatalog(_db, _timeProvider);
            await catalog.LoadAsync(ct);
            await _db.Cards.Where(c => c.GameId == catalog.Game.Id).LoadAsync(ct);
            var printings = await _db.CardPrintings
                .Include(p => p.Card)
                .Include(p => p.Treatment)
                .Where(p => p.Card.GameId == catalog.Game.Id).ToListAsync(ct);
            var identity = new SwuSharedIdentity(printings);
            var cardChanges = new SwuSharedCardChanges(summary);
            var autoDetectChangesEnabled = _db.ChangeTracker.AutoDetectChangesEnabled;
            _db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                int processed = 0;
                foreach (var record in records.Take(Math.Max(0, options.Limit ?? int.MaxValue)))
                {
                    ct.ThrowIfCancellationRequested();
                    processed++;
                    var counts = (summary.CardsCreated, summary.CardsUpdated,
                        summary.PrintingsCreated, summary.PrintingsUpdated);
                    try
                    {
                        var card = Upsert(record, summary, identity, requireExpansion, catalog,
                            out var associationsChanged);
                        var entry = card is null ? null : _db.Entry(card);
                        var state = entry?.State ?? EntityState.Unchanged;
                        var cardChanged = state == EntityState.Modified
                            && entry!.Properties.Any(property => property.IsModified
                                && property.Metadata.Name != nameof(Card.LastSyncedAt));
                        // EF rolls back failed writes to a savepoint in the import transaction.
                        await _db.SaveChangesAsync(ct);
                        if (state == EntityState.Added) cardChanges.Created(card!);
                        if (cardChanged || associationsChanged)
                            cardChanges.Updated(card!);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Failed to import SWU card record {SourceId}.", record?.Id);
                        // A failed record can include lookup/association changes as well as scalars.
                        // Discard all unsaved tracked changes; committed records remain in the transaction.
                        _db.ChangeTracker.Clear();
                        await catalog.LoadAsync(ct);
                        await _db.Cards.Where(c => c.GameId == catalog.Game.Id).LoadAsync(ct);
                        printings = await _db.CardPrintings.Include(p => p.Card)
                            .Include(p => p.Treatment)
                            .Where(p => p.Card.GameId == catalog.Game.Id).ToListAsync(ct);
                        identity = new SwuSharedIdentity(printings);
                        (summary.CardsCreated, summary.CardsUpdated,
                            summary.PrintingsCreated, summary.PrintingsUpdated) = counts;
                        summary.Errors++;
                        var attrs = record?.Attributes;
                        summary.Messages.Add(
                            $"Error [id={record?.Id} serialCode={attrs?.SerialCode}] {attrs?.Title}: Record could not be imported.");
                    }
                }

                identity.ResolveRelationships(out var changedPrintings);

                foreach (var printing in changedPrintings)
                {
                    var entry = _db.Entry(printing);
                    if (entry.State == EntityState.Added) continue;
                    entry.Property(entity => entity.VariantOfId).IsModified = true;
                    entry.Property(entity => entity.ReprintOfId).IsModified = true;
                }
                await _db.SaveChangesAsync(ct);
                summary.Messages.Add($"Processed {processed} records {description}.");
                return summary;
            }
            finally
            {
                _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
            }
        });
    }

    private Card? Upsert(StrapiRecord record, ImportSummary summary, SwuSharedIdentity identity,
        bool requireExpansion, SwuCatalog catalog, out bool associationsChanged)
    {
        associationsChanged = false;

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

        var expansion = attrs.Expansion?.Data?.Attributes;
        if (requireExpansion && (string.IsNullOrWhiteSpace(expansion?.Code) || string.IsNullOrWhiteSpace(expansion.Name))
            || expansion?.Code?.Trim().Length > 32 || expansion?.Name?.Trim().Length > 256)
            throw new InvalidOperationException("A valid expansion code and name are required.");

        string name = string.IsNullOrWhiteSpace(attrs.Subtitle)
            ? attrs.Title?.Trim() ?? "Unknown"
            : $"{attrs.Title?.Trim()} \u2014 {attrs.Subtitle.Trim()}";
        string? text = attrs.Text;
        string set = expansion?.Code?.Trim().ToUpperInvariant() ?? "UNK";
        string number = attrs.CardNumber?.ToString() ?? record.Id.ToString();
        string? serialCode = string.IsNullOrWhiteSpace(attrs.SerialCode) ? null : attrs.SerialCode.Trim();
        string? cardUid = string.IsNullOrWhiteSpace(attrs.CardUid) ? null : attrs.CardUid.Trim();
        int sourceId = record.Id;
        string rarity = attrs.Rarity ?? "Unknown";

        string? variantOfCardUid = attrs.VariantOf?.Data?.Attributes?.CardUid;
        string? reprintOfCardUid = attrs.ReprintOf?.Data?.Attributes?.CardUid;

        var variantType = attrs.VariantTypes?.Data?
            .Select(variant => variant.Attributes)
            .FirstOrDefault(attributes => !string.IsNullOrWhiteSpace(attributes?.Name));
        string treatmentName = variantType?.Name?.Trim() ?? "Standard";
        bool isFoil = attrs.VariantTypes?.Data?.Any(vt => vt.Attributes?.Foil == true) == true;

        string? imageUrl = attrs.ArtFront?.Data?.Attributes?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Card?.Url
                        ?? attrs.ArtFront?.Data?.Attributes?.Formats?.Thumbnail?.Url;

        // CardUid identifies a printing; set/number/treatment is the fallback identity.
        var printing = identity.FindPrinting(cardUid, set, number, treatmentName);
        var card = _db.Cards.Local.FirstOrDefault(c => c.GameId == catalog.Game.Id && c.Name == name)
            ?? printing?.Card;

        bool isBasePrinting = string.IsNullOrWhiteSpace(variantOfCardUid)
            && string.IsNullOrWhiteSpace(reprintOfCardUid);
        bool isCanonicalForCard = card is null
            || !card.Printings.Any(p => p != printing && IsHigherPriorityPrinting(p, isBasePrinting, sourceId));

        var printingJson = JsonSerializer.Serialize(new
        {
            cardUid,
            serialCode,
            cardNumber = attrs.CardNumber,
            variantTypes = attrs.VariantTypes?.Data?.Select(v => new
            {
                v.Id,
                v.Attributes?.Name,
                v.Attributes?.VariantId,
                v.Attributes?.Foil
            }).OrderBy(v => v.Id),
            variantOfCardUid,
            reprintOfCardUid
        }, JsonOptions);

        // All preparation above must finish before changing tracked entities.
        if (card is null)
        {
            card = new Card
            {
                Game = catalog.Game,
                Name = name,
                Text = text,
                CardUid = cardUid
            };
            _db.Cards.Add(card);
        }
        else if (isCanonicalForCard)
        {
            bool changed = card.Name != name
                || card.Text != text;
            card.Name = name;
            card.Text = text;
            if (changed && _db.Entry(card).State != EntityState.Added)
                _db.Entry(card).State = EntityState.Modified;
        }

        if (isCanonicalForCard)
        {
            var originalCardValues = _db.Entry(card).CurrentValues.Clone();
            card.CardUid = cardUid;
            card.Subtitle = attrs.Subtitle;
            card.TextStyled = attrs.TextStyled;
            card.DeployBox = attrs.DeployBox;
            card.DeployBoxStyled = attrs.DeployBoxStyled;
            card.EpicAction = attrs.EpicAction;
            card.EpicActionStyled = attrs.EpicActionStyled;
            card.Rules = attrs.Rules;
            card.RulesStyled = attrs.RulesStyled;
            card.Cost = attrs.Cost;
            card.Power = attrs.Power;
            card.Hp = attrs.Health ?? attrs.Hp;
            card.UpgradeHp = attrs.UpgradeHp;
            card.UpgradePower = attrs.UpgradePower;
            if (_db.Entry(card).State != EntityState.Added
                && originalCardValues.Properties.Any(p => !Equals(originalCardValues[p], _db.Entry(card).CurrentValues[p])))
                _db.Entry(card).State = EntityState.Modified;
        }

        var syncedAt = _timeProvider.GetUtcNow();
        card.LastSyncedAt = syncedAt;
        if (_db.Entry(card).State != EntityState.Added)
            _db.Entry(card).Property(entity => entity.LastSyncedAt).IsModified = true;

        var printingCreated = printing is null;
        if (printing is null)
        {
            printing = new CardPrinting
            {
                Card = card,
                Set = set,
                Number = number,
                Rarity = rarity,
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
            if (printing.Number != number) { printing.Number = number; changed = true; }
            if (printing.Rarity != rarity) { printing.Rarity = rarity; changed = true; }
            if (printing.ImageUrl != imageUrl) { printing.ImageUrl = imageUrl; changed = true; }
            if (printing.DetailsJson != printingJson) { printing.DetailsJson = printingJson; changed = true; }
            if (changed)
            {
                summary.PrintingsUpdated++;
                if (_db.Entry(printing).State != EntityState.Added)
                    _db.Entry(printing).State = EntityState.Modified;
            }
        }
        var updateCounted = !printingCreated && _db.Entry(printing).State == EntityState.Modified;
        var originalPrintingValues = _db.Entry(printing).CurrentValues.Clone();
        var oldCard = printing.Card;
        _db.Entry(printing).Reference(entity => entity.Card).CurrentValue = card;
        if (oldCard != card)
        {
            oldCard.Printings.Remove(printing);
            if (!card.Printings.Contains(printing))
                card.Printings.Add(printing);
            if (oldCard.Printings.Count == 0)
                _db.Cards.Remove(oldCard);
        }
        else if (!card.Printings.Contains(printing))
        {
            card.Printings.Add(printing);
        }
        printing.StrapiId = sourceId;
        printing.CardUid = cardUid;
        printing.BackImageUrl = attrs.ArtBack?.Data?.Attributes?.Url
            ?? attrs.ArtBack?.Data?.Attributes?.Formats?.Card?.Url
            ?? attrs.ArtBack?.Data?.Attributes?.Formats?.Thumbnail?.Url;
        printing.ArtFrontHorizontal = attrs.ArtFrontHorizontal;
        printing.ArtBackHorizontal = attrs.ArtBackHorizontal;
        printing.Artist = attrs.Artist;
        printing.IsFoil = isFoil;
        printing.ApiCreatedAt = attrs.CreatedAt?.ToUniversalTime();
        printing.ApiUpdatedAt = attrs.UpdatedAt?.ToUniversalTime();
        printing.ApiPublishedAt = attrs.PublishedAt?.ToUniversalTime();
        associationsChanged = catalog.Apply(card, printing, attrs, includeCardAssociations: isCanonicalForCard);
        if (!printingCreated && !updateCounted
            && (_db.Entry(printing).State == EntityState.Modified || originalPrintingValues.Properties.Any(p =>
                p.Name != nameof(CardPrinting.LastSyncedAt)
                && !Equals(originalPrintingValues[p], _db.Entry(printing).CurrentValues[p]))))
        {
            summary.PrintingsUpdated++;
            _db.Entry(printing).State = EntityState.Modified;
        }
        printing.LastSyncedAt = syncedAt;
        if (_db.Entry(printing).State != EntityState.Added)
            _db.Entry(printing).Property(entity => entity.LastSyncedAt).IsModified = true;
        identity.Track(printing);
        return card;
    }

    private static bool IsHigherPriorityPrinting(CardPrinting existing, bool candidateIsBase, int candidateSourceId)
    {
        bool existingIsBase = !SwuSharedIdentity.IsVariantOrReprint(existing);
        if (existingIsBase != candidateIsBase)
            return existingIsBase;
        var existingSourceId = SwuSharedIdentity.SourceId(existing) ?? int.MaxValue;
        return existingSourceId < candidateSourceId;
    }
}
