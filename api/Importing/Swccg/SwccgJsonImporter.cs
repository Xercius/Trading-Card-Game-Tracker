using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Swccg;

/// <summary>
/// Imports Star Wars CCG cards from swccg-card-json and images from holotable.
/// </summary>
public sealed class SwccgJsonImporter : ISourceImporter
{
    public const string ImporterKey = "swccg";
    public string Key => ImporterKey;
    public string DisplayName => "Star Wars CCG (swccg-card-json / holotable)";
    public IEnumerable<string> SupportedGames => new[] { "Star Wars CCG" };

    private readonly AppDbContext _db;
    private readonly ISwccgApiClient _client;
    private readonly ILogger<SwccgJsonImporter> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SwccgJsonImporter(
        AppDbContext db,
        ISwccgApiClient client,
        ILogger<SwccgJsonImporter> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _client = client;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun
        };

        var sets = await _client.GetSetsAsync(ct);
        var cards = await _client.GetAllCardsAsync(ct);

        if (!string.IsNullOrWhiteSpace(options.SetCode))
        {
            var filter = options.SetCode.Trim();
            var matchingSet = sets.FirstOrDefault(s =>
                string.Equals(s.Id, filter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Abbr, filter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Name, filter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.GempName, filter, StringComparison.OrdinalIgnoreCase));

            cards = cards.Where(c =>
                string.Equals(c.Set, filter, StringComparison.OrdinalIgnoreCase)
                || (matchingSet != null && (
                    string.Equals(c.Set, matchingSet.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Set, matchingSet.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Set, matchingSet.GempName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Set, matchingSet.Abbr, StringComparison.OrdinalIgnoreCase)))
                || (c.GempId != null && c.GempId.StartsWith((matchingSet?.Id ?? filter) + "_", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return await ImportRecordsAsync(cards, sets, options, summary, ct);
    }

    public async Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = options.DryRun
        };

        using var doc = await JsonDocument.ParseAsync(file, cancellationToken: ct);
        List<SwccgCardRecord> records;
        List<SwccgSet> sets = [];

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            records = JsonSerializer.Deserialize<List<SwccgCardRecord>>(doc.RootElement.GetRawText(), JsonOptions)
                      ?? throw new InvalidOperationException("Empty array in file.");
        }
        else if (doc.RootElement.TryGetProperty("cards", out var cardsElement))
        {
            records = JsonSerializer.Deserialize<List<SwccgCardRecord>>(cardsElement.GetRawText(), JsonOptions)
                      ?? throw new InvalidOperationException("Empty cards array in file.");
            if (doc.RootElement.TryGetProperty("sets", out var setsElement))
            {
                sets = JsonSerializer.Deserialize<List<SwccgSet>>(setsElement.GetRawText(), JsonOptions) ?? [];
            }
        }
        else
        {
            throw new InvalidOperationException("Unrecognized SWCCG JSON structure.");
        }

        return await ImportRecordsAsync(records, sets, options, summary, ct);
    }

    private async Task<ImportSummary> ImportRecordsAsync(
        List<SwccgCardRecord> records,
        List<SwccgSet> sets,
        ImportOptions options,
        ImportSummary summary,
        CancellationToken ct)
    {
        var catalog = new SwccgCatalog(_db, _timeProvider);
        catalog.InitializeSetMetadata(sets);

        return await _db.WithDryRunAsync(options.DryRun, async () =>
        {
            await catalog.LoadAsync(ct);
            var game = catalog.Game;

            var existingCards = await _db.Cards.Where(c => c.GameId == game.Id).ToListAsync(ct);
            var cardsByUid = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
            var existingCardSet = new HashSet<Card>(ReferenceEqualityComparer.Instance);
            foreach (var c in existingCards)
            {
                existingCardSet.Add(c);
                if (!string.IsNullOrWhiteSpace(c.CardUid))
                    cardsByUid[c.CardUid] = c;
            }

            var printings = await _db.CardPrintings
                .Include(p => p.Card)
                .Include(p => p.CatalogSet)
                .Include(p => p.Treatment)
                .Where(p => p.Card.GameId == game.Id)
                .ToListAsync(ct);

            var identity = new SwccgSharedIdentity(printings);
            var updatedCards = new HashSet<Card>(ReferenceEqualityComparer.Instance);
            var updatedPrintings = new HashSet<int>();
            int processed = 0;
            int limit = options.Limit ?? int.MaxValue;

            var previousAutoDetect = _db.ChangeTracker.AutoDetectChangesEnabled;
            try
            {
                _db.ChangeTracker.AutoDetectChangesEnabled = false;

                foreach (var record in records)
                {
                    if (processed++ >= limit) break;

                    try
                    {
                        UpsertCard(record, catalog, identity, cardsByUid, existingCardSet, updatedCards, summary, updatedPrintings);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Failed to import SWCCG card record {SourceId} ({Title}).",
                            record.Id, record.Front?.Title);
                        summary.Errors++;
                        summary.Messages.Add($"Failed to import SWCCG record [{record.Id}].");
                    }
                }
            }
            finally
            {
                _db.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetect;
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Processed {Math.Min(processed, records.Count)} SWCCG cards.");
            return summary;
        });
    }

    private void UpsertCard(
        SwccgCardRecord record,
        SwccgCatalog catalog,
        SwccgSharedIdentity identity,
        Dictionary<string, Card> cardsByUid,
        HashSet<Card> existingCardSet,
        HashSet<Card> updatedCards,
        ImportSummary summary,
        HashSet<int> updatedPrintings)
    {
        var face = record.Front ?? record.Back;
        if (face is null || string.IsNullOrWhiteSpace(face.Title))
        {
            throw new InvalidOperationException($"Record ID {record.Id} missing face title.");
        }

        var (cleanTitle, inferredUniqueness) = SwccgCatalog.ParseTitleUniqueness(face.Title);
        var title = !string.IsNullOrWhiteSpace(cleanTitle) ? cleanTitle : face.Title.Trim();
        var cardUid = !string.IsNullOrWhiteSpace(record.GempId)
            ? record.GempId.Trim()
            : record.Id.ToString();

        var set = catalog.GetOrAddSet(record.Set);
        var number = ParseNumber(record.GempId) ?? record.Id.ToString();

        // 1. Resolve Printing
        var printing = identity.FindPrinting(cardUid, set, number);

        // 2. Resolve Card
        var card = printing?.Card;
        if (card is null)
        {
            cardsByUid.TryGetValue(cardUid, out card);
        }

        var now = _timeProvider.GetUtcNow();
        var isNewCard = card is null;

        if (isNewCard)
        {
            card = new Card
            {
                Game = catalog.Game,
                Name = title,
                CardUid = cardUid
            };
            _db.Cards.Add(card);
            cardsByUid[cardUid] = card;
            summary.CardsCreated++;
        }

        // Apply card properties
        card!.Name = title;
        card.CardUid = cardUid;
        card.Text = face.Gametext;
        card.TextStyled = face.Gametext;
        card.Lore = face.Lore;
        card.Side = record.Side;
        card.Uniqueness = !string.IsNullOrWhiteSpace(face.Uniqueness)
            ? face.Uniqueness
            : inferredUniqueness;
        card.Destiny = face.Destiny;
        card.Deploy = face.Deploy;
        card.Forfeit = face.Forfeit;
        card.Armor = face.Armor;
        card.Ferocity = face.Ferocity;
        card.Maneuver = TryParseInt(face.Maneuver);
        card.Hyperspeed = TryParseInt(face.Hyperspeed);
        card.Landspeed = TryParseInt(face.Landspeed);
        card.Parsec = TryParseInt(face.Parsec);
        card.Politics = TryParseInt(face.Politics);
        card.Ability = TryParseInt(face.Ability);
        card.Power = TryParseInt(face.Power);
        card.Cost = TryParseInt(face.Deploy);
        card.Persona = record.Personas is { Count: > 0 } ? string.Join(", ", record.Personas) : null;
        card.RulesStyled = SwccgCatalog.BuildRulesStyled(record);
        card.Rules = record.Rulings is { Count: > 0 } ? string.Join("\n", record.Rulings) : null;
        card.ExtraCardDataJson = SwccgCatalog.BuildExtraCardDataJson(record);
        card.BackTitle = SwccgCatalog.CleanTitle(record.Back?.Title);
        card.BackText = record.Back?.Gametext;
        card.LightSideIcons = face.LightSideIcons;
        card.DarkSideIcons = face.DarkSideIcons;
        card.BackLightSideIcons = record.Back?.LightSideIcons;
        card.BackDarkSideIcons = record.Back?.DarkSideIcons;
        card.LastSyncedAt = now;

        if (!isNewCard && existingCardSet.Contains(card) && updatedCards.Add(card))
        {
            summary.CardsUpdated++;
        }

        // 3. Printing details
        var isNewPrinting = printing is null;
        var imageUrl = record.Front?.ImageUrl;
        var backImageUrl = record.Back?.ImageUrl;
        var isHorizontal = face.Type?.Equals("Location", StringComparison.OrdinalIgnoreCase) == true
            || face.SubType?.Contains("Site", StringComparison.OrdinalIgnoreCase) == true
            || face.SubType?.Contains("System", StringComparison.OrdinalIgnoreCase) == true;

        if (isNewPrinting)
        {
            printing = new CardPrinting
            {
                Card = card,
                Number = number,
                CardUid = cardUid,
                ImageUrl = imageUrl,
                BackImageUrl = backImageUrl,
                ArtFrontHorizontal = isHorizontal,
                ArtBackHorizontal = record.Back?.Type?.Equals("Location", StringComparison.OrdinalIgnoreCase),
                IsFoil = record.Rarity?.Contains("Foil", StringComparison.OrdinalIgnoreCase) == true,
                DetailsJson = card.ExtraCardDataJson,
                LastSyncedAt = now
            };
            _db.CardPrintings.Add(printing);
            identity.Register(printing);
            summary.PrintingsCreated++;
        }
        else
        {
            printing!.ImageUrl = imageUrl;
            printing.BackImageUrl = backImageUrl;
            printing.ArtFrontHorizontal = isHorizontal;
            printing.ArtBackHorizontal = record.Back?.Type?.Equals("Location", StringComparison.OrdinalIgnoreCase);
            printing.DetailsJson = card.ExtraCardDataJson;
            printing.LastSyncedAt = now;

            if (updatedPrintings.Add(printing.Id))
            {
                summary.PrintingsUpdated++;
            }
        }

        catalog.Apply(card, printing, record);
    }

    private static string? ParseNumber(string? gempId)
    {
        if (string.IsNullOrWhiteSpace(gempId)) return null;
        var i = gempId.IndexOf('_');
        return i >= 0 && i < gempId.Length - 1 ? gempId[(i + 1)..] : gempId;
    }

    private static int? TryParseInt(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        return int.TryParse(val.Trim(), out var result) ? result : null;
    }
}
