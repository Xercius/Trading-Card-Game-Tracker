using System.Security.Cryptography;
using System.Text;
using api.Data;
using api.Features.Admin.Sync.Dtos;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Swccg;

/// <summary>
/// Imports SWCCG new cards and alternate printings from repository CSV files with per-row failure reporting.
/// </summary>
public sealed class SwccgAlternatePrintingsImporter
{
    internal const string NewCardsFileName = "SWCCG_NewCards.csv";
    internal const string AlternatePrintingsFileName = "SWCCG_AlternatePrintings.csv";

    private sealed record CardLookupEntry(Card Card, CardPrinting? BasePrinting);

    private sealed class ImportCounters
    {
        public int TotalRowsProcessed { get; set; }
        public int NewCardsCreated { get; set; }
        public int PrintingsCreated { get; set; }
        public int SkippedExisting { get; set; }
        public List<SwccgCsvRowIssue> Failures { get; } = [];
    }

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SwccgAlternatePrintingsImporter> _logger;

    public SwccgAlternatePrintingsImporter(
        AppDbContext db,
        TimeProvider timeProvider,
        IWebHostEnvironment env,
        ILogger<SwccgAlternatePrintingsImporter> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _env = env;
        _logger = logger;
    }

    /// <summary>Imports SWCCG new cards and alternate printings from the repository CSV files.</summary>
    public async Task<AdminSwccgAlternatePrintingsSyncResponse> ImportFromRepoFilesAsync(
        bool dryRun,
        CancellationToken ct = default)
    {
        var newCardsPath = ResolveCsvPath(NewCardsFileName);
        var altPrintingsPath = ResolveCsvPath(AlternatePrintingsFileName);

        await using var newCardsStream = File.OpenRead(newCardsPath);
        await using var altPrintingsStream = File.OpenRead(altPrintingsPath);
        return await ImportFromStreamsAsync(newCardsStream, altPrintingsStream, dryRun, ct);
    }

    /// <summary>Imports SWCCG new cards and alternate printings from explicit CSV streams.</summary>
    public async Task<AdminSwccgAlternatePrintingsSyncResponse> ImportFromStreamsAsync(
        Stream newCardsCsv,
        Stream alternatePrintingsCsv,
        bool dryRun,
        CancellationToken ct = default)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var newCardRows = SwccgCsvReader.ReadRows(newCardsCsv);
        var altPrintingRows = SwccgCsvReader.ReadRows(alternatePrintingsCsv);

        return await _db.WithDryRunAsync(dryRun, async () =>
        {
            var catalog = new SwccgCatalog(_db, _timeProvider);
            await catalog.LoadAsync(ct);

            var initialSets = catalog.SetCount;
            var initialRarities = catalog.RarityCount;
            var initialTreatments = catalog.TreatmentCount;

            var existingCards = await _db.Cards
                .Include(c => c.Printings).ThenInclude(p => p.CatalogSet)
                .Include(c => c.Printings).ThenInclude(p => p.Treatment)
                .Include(c => c.Printings).ThenInclude(p => p.RarityLookup)
                .Where(c => c.GameId == catalog.Game.Id)
                .ToListAsync(ct);

            var counters = new ImportCounters();
            ImportNewCards(newCardRows, catalog, existingCards, counters);
            ImportAlternatePrintings(altPrintingRows, catalog, existingCards, counters);

            await _db.SaveChangesAsync(ct);

            var completedAt = _timeProvider.GetUtcNow();
            return BuildResponse(
                dryRun,
                startedAt,
                completedAt,
                counters,
                setsCreated: catalog.SetCount - initialSets,
                raritiesCreated: catalog.RarityCount - initialRarities,
                treatmentsCreated: catalog.TreatmentCount - initialTreatments);
        });
    }

    private void ImportNewCards(
        List<(int RowNumber, Dictionary<string, string> Fields)> rows,
        SwccgCatalog catalog,
        List<Card> existingCards,
        ImportCounters counters)
    {
        var now = _timeProvider.GetUtcNow();
        var existingByUid = existingCards
            .Where(c => !string.IsNullOrWhiteSpace(c.CardUid))
            .ToDictionary(c => c.CardUid!, StringComparer.OrdinalIgnoreCase);

        foreach (var (rowNumber, fields) in rows)
        {
            counters.TotalRowsProcessed++;
            var cardName = GetField(fields, "Card Name");
            var side = GetField(fields, "Side");
            var expansion = GetField(fields, "Expansion");
            var cardType = GetField(fields, "Card Type");
            var rawTreatment = GetField(fields, "Treatment");
            var notes = GetField(fields, "Notes");
            var rawRarity = GetField(fields, "Rarity");

            if (string.IsNullOrWhiteSpace(cardName) || string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(expansion))
            {
                counters.Failures.Add(new SwccgCsvRowIssue(
                    NewCardsFileName, rowNumber, cardName, side, GetField(fields, "Original Set"), expansion,
                    "Missing required Card Name, Side, or Expansion."));
                continue;
            }

            var uid = ComputeDeterministicUid("swccg-csv:new", cardName, side, expansion);
            var expansionSet = catalog.GetOrAddSet(expansion);
            var treatmentName = ResolveCsvTreatment(rawTreatment);
            var treatment = catalog.GetOrAddTreatment(treatmentName);
            var rarity = catalog.GetOrAddRarity(rawRarity);

            var card = FindExistingNewCard(existingCards, existingByUid, uid, cardName, side);
            if (card is not null && HasMatchingPrinting(card, uid, expansionSet, string.Empty, treatment))
            {
                counters.SkippedExisting++;
                continue;
            }

            if (card is null)
            {
                card = new Card
                {
                    Game = catalog.Game,
                    Name = cardName,
                    Side = side,
                    CardUid = uid,
                    LastSyncedAt = now
                };
                catalog.ApplyCsvNewCardAssociations(card, cardType, side);
                _db.Cards.Add(card);
                existingCards.Add(card);
                existingByUid[uid] = card;
                counters.NewCardsCreated++;
            }

            var printing = new CardPrinting
            {
                Card = card,
                CatalogSet = expansionSet,
                Treatment = treatment,
                RarityLookup = rarity,
                Number = string.Empty,
                ImageUrl = null,
                IsFoil = IsCsvFoil(treatmentName, rawRarity),
                PrintingNotes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                CardUid = uid,
                LastSyncedAt = now
            };
            card.Printings.Add(printing);
            _db.CardPrintings.Add(printing);
            counters.PrintingsCreated++;
        }
    }

    private void ImportAlternatePrintings(
        List<(int RowNumber, Dictionary<string, string> Fields)> rows,
        SwccgCatalog catalog,
        List<Card> existingCards,
        ImportCounters counters)
    {
        var now = _timeProvider.GetUtcNow();
        var (strictIndex, relaxedIndex, namesByScope) = BuildBaseCardIndexes(existingCards);

        foreach (var (rowNumber, fields) in rows)
        {
            counters.TotalRowsProcessed++;
            var cardName = GetField(fields, "Card Name");
            var side = GetField(fields, "Side");
            var expansion = GetField(fields, "Expansion");
            var rawTreatment = GetField(fields, "Treatment");
            var originalSet = GetField(fields, "Original Set");
            var border = GetField(fields, "Border");
            var notes = GetField(fields, "Notes");
            var rawRarity = GetField(fields, "Rarity");

            if (string.IsNullOrWhiteSpace(cardName) || string.IsNullOrWhiteSpace(side)
                || string.IsNullOrWhiteSpace(expansion) || string.IsNullOrWhiteSpace(originalSet))
            {
                counters.Failures.Add(new SwccgCsvRowIssue(
                    AlternatePrintingsFileName, rowNumber, cardName, side, originalSet, expansion,
                    "Missing required Card Name, Side, Expansion, or Original Set."));
                continue;
            }

            var (match, failureReason) = ResolveBaseCard(cardName, side, originalSet, strictIndex, relaxedIndex, namesByScope);
            if (match is null)
            {
                counters.Failures.Add(new SwccgCsvRowIssue(
                    AlternatePrintingsFileName, rowNumber, cardName, side, originalSet, expansion, failureReason!));
                continue;
            }

            var expansionSet = catalog.GetOrAddSet(expansion);
            var treatmentName = ResolveCsvTreatment(rawTreatment);
            var treatment = catalog.GetOrAddTreatment(treatmentName);
            var rarity = catalog.GetOrAddRarity(rawRarity);
            var inheritedNumber = match.BasePrinting?.Number ?? string.Empty;
            var cardIdentity = !string.IsNullOrWhiteSpace(match.Card.CardUid)
                ? match.Card.CardUid
                : $"{match.Card.Name}|{match.Card.Side}";
            var uid = ComputeDeterministicUid("swccg-csv:alt", cardIdentity, expansionSet.Code, treatment.Name, border, notes);

            if (HasMatchingPrinting(match.Card, uid, expansionSet, inheritedNumber, treatment, notes))
            {
                counters.SkippedExisting++;
                continue;
            }

            var printing = new CardPrinting
            {
                Card = match.Card,
                CatalogSet = expansionSet,
                Treatment = treatment,
                RarityLookup = rarity,
                Number = inheritedNumber,
                ImageUrl = match.BasePrinting?.ImageUrl,
                BackImageUrl = match.BasePrinting?.BackImageUrl,
                ArtFrontHorizontal = match.BasePrinting?.ArtFrontHorizontal,
                ArtBackHorizontal = match.BasePrinting?.ArtBackHorizontal,
                IsFoil = IsCsvFoil(treatmentName, rawRarity),
                PrintingNotes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                CardUid = uid,
                LastSyncedAt = now
            };
            match.Card.Printings.Add(printing);
            _db.CardPrintings.Add(printing);
            counters.PrintingsCreated++;
        }
    }

    private static (
        Dictionary<(string Name, string Side, string Set), List<CardLookupEntry>> Strict,
        Dictionary<(string Name, string Side, string Set), List<CardLookupEntry>> Relaxed,
        Dictionary<(string Side, string Set), List<string>> NamesByScope)
        BuildBaseCardIndexes(List<Card> existingCards)
    {
        var strict = new Dictionary<(string, string, string), List<CardLookupEntry>>();
        var relaxed = new Dictionary<(string, string, string), List<CardLookupEntry>>();
        var namesByScope = new Dictionary<(string, string), List<string>>();

        foreach (var card in existingCards)
        {
            var sideKey = NormalizeSideKey(card.Side);
            var strictName = SwccgCatalog.StripAllUniquenessMarkers(card.Name).ToLowerInvariant();
            var relaxedName = SwccgCatalog.NormalizeForRelaxedMatch(card.Name);

            foreach (var printing in card.Printings
                .OrderBy(p => string.Equals(p.Treatment?.Name, "Standard", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p.IsFoil ? 1 : 0)
                .ThenBy(p => p.Id))
            {
                if (printing.CatalogSet is null) continue;
                var entry = new CardLookupEntry(card, printing);

                foreach (var setRaw in new[] { printing.CatalogSet.Name, printing.CatalogSet.Code })
                {
                    var setKey = SwccgCatalog.NormalizeSetMatchKey(setRaw);
                    if (string.IsNullOrEmpty(setKey)) continue;

                    AddToIndex(strict, (strictName, sideKey, setKey), entry);
                    AddToIndex(relaxed, (relaxedName, sideKey, setKey), entry);

                    var scopeKey = (sideKey, setKey);
                    if (!namesByScope.TryGetValue(scopeKey, out var names))
                    {
                        names = [];
                        namesByScope[scopeKey] = names;
                    }
                    names.Add(card.Name);
                }
            }
        }

        return (strict, relaxed, namesByScope);
    }

    private static void AddToIndex(
        Dictionary<(string Name, string Side, string Set), List<CardLookupEntry>> index,
        (string Name, string Side, string Set) key,
        CardLookupEntry entry)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index[key] = list;
        }
        if (!list.Any(e => ReferenceEquals(e.Card, entry.Card)))
            list.Add(entry);
    }

    private static (CardLookupEntry? Match, string? Reason) ResolveBaseCard(
        string cardName,
        string side,
        string originalSet,
        Dictionary<(string Name, string Side, string Set), List<CardLookupEntry>> strictIndex,
        Dictionary<(string Name, string Side, string Set), List<CardLookupEntry>> relaxedIndex,
        Dictionary<(string Side, string Set), List<string>> namesByScope)
    {
        var sideKey = NormalizeSideKey(side);
        var setKey = SwccgCatalog.NormalizeSetMatchKey(originalSet);
        var strictName = SwccgCatalog.StripAllUniquenessMarkers(cardName).ToLowerInvariant();

        if (strictIndex.TryGetValue((strictName, sideKey, setKey), out var strictHits))
        {
            if (strictHits.Count == 1)
                return (strictHits[0], null);
            return (null, $"Ambiguous exact match ({strictHits.Count} cards) for '{cardName}' ({side}, {originalSet}).");
        }

        var relaxedName = SwccgCatalog.NormalizeForRelaxedMatch(cardName);
        if (relaxedIndex.TryGetValue((relaxedName, sideKey, setKey), out var relaxedHits))
        {
            if (relaxedHits.Count == 1)
                return (relaxedHits[0], null);
            return (null, $"Ambiguous relaxed match ({relaxedHits.Count} cards) for '{cardName}' ({side}, {originalSet}).");
        }

        var scopeNames = namesByScope.TryGetValue((sideKey, setKey), out var list) ? list : [];
        var closest = SwccgCsvReader.FindClosestMatches(cardName, scopeNames);
        var suggestion = closest.Count > 0 ? $" Closest in set: {string.Join(", ", closest)}" : string.Empty;
        return (null, $"No matching base card found for '{cardName}' ({side}, {originalSet}).{suggestion}");
    }

    private static Card? FindExistingNewCard(
        List<Card> existingCards,
        Dictionary<string, Card> existingByUid,
        string uid,
        string cardName,
        string side)
    {
        if (existingByUid.TryGetValue(uid, out var byUid))
            return byUid;

        var normalizedSide = NormalizeSideKey(side);
        return existingCards.FirstOrDefault(c =>
            string.Equals(c.Name.Trim(), cardName.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeSideKey(c.Side), normalizedSide, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasMatchingPrinting(
        Card card,
        string uid,
        Set expansionSet,
        string number,
        Treatment treatment,
        string? notes = null)
    {
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return card.Printings.Any(p =>
            string.Equals(p.CardUid, uid, StringComparison.OrdinalIgnoreCase)
            || (p.SetId == expansionSet.Id
                && expansionSet.Id > 0
                && string.Equals(p.Number, number, StringComparison.OrdinalIgnoreCase)
                && p.TreatmentId == treatment.Id
                && treatment.Id > 0
                && string.Equals(p.PrintingNotes?.Trim(), normalizedNotes, StringComparison.OrdinalIgnoreCase))
            || (ReferenceEquals(p.CatalogSet, expansionSet)
                && string.Equals(p.Number, number, StringComparison.OrdinalIgnoreCase)
                && ReferenceEquals(p.Treatment, treatment)
                && string.Equals(p.PrintingNotes?.Trim(), normalizedNotes, StringComparison.OrdinalIgnoreCase)));
    }

    private AdminSwccgAlternatePrintingsSyncResponse BuildResponse(
        bool dryRun,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        ImportCounters counters,
        int setsCreated,
        int raritiesCreated,
        int treatmentsCreated)
    {
        var failedCount = counters.Failures.Count;
        var status = failedCount == 0 ? "Succeeded" : "CompletedWithWarnings";
        var messages = new List<string>
        {
            $"Processed {counters.TotalRowsProcessed} CSV rows: {counters.NewCardsCreated} new cards, {counters.PrintingsCreated} printings created, {counters.SkippedExisting} skipped, {failedCount} failed."
        };
        if (failedCount > 0)
        {
            messages.Add($"{failedCount} row(s) could not be imported; review the Failures table for details.");
            _logger.LogWarning("SWCCG alternate printings sync completed with {FailedCount} failed rows.", failedCount);
        }

        return new AdminSwccgAlternatePrintingsSyncResponse(
            Source: "swccg-alt-csv",
            Status: status,
            DryRun: dryRun,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            TotalRowsProcessed: counters.TotalRowsProcessed,
            NewCardsCreated: counters.NewCardsCreated,
            PrintingsCreated: counters.PrintingsCreated,
            SkippedExisting: counters.SkippedExisting,
            SetsCreated: Math.Max(0, setsCreated),
            RaritiesCreated: Math.Max(0, raritiesCreated),
            TreatmentsCreated: Math.Max(0, treatmentsCreated),
            FailedCount: failedCount,
            Failures: counters.Failures,
            Messages: messages);
    }

    private string ResolveCsvPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Importing", "Swccg", fileName),
            Path.Combine(_env.ContentRootPath, "Importing", "Swccg", fileName),
            Path.Combine(_env.ContentRootPath, "api", "Importing", "Swccg", fileName)
        };

        var match = candidates.FirstOrDefault(File.Exists);
        return match ?? throw new FileNotFoundException($"SWCCG CSV file '{fileName}' was not found.", candidates[0]);
    }

    private static string ResolveCsvTreatment(string rawTreatment)
        => string.IsNullOrWhiteSpace(rawTreatment) ? "Standard" : rawTreatment.Trim();

    private static bool IsCsvFoil(string treatmentName, string rawRarity)
        => treatmentName.Contains("Foil", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawRarity.Trim(), "TF", StringComparison.OrdinalIgnoreCase)
            || SwccgCatalog.IsFoilRarity(rawRarity);

    private static string NormalizeSideKey(string? side)
    {
        if (string.IsNullOrWhiteSpace(side)) return string.Empty;
        var trimmed = side.Trim();
        if (trimmed.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)) return "dark";
        if (trimmed.StartsWith("Light", StringComparison.OrdinalIgnoreCase)) return "light";
        return trimmed.ToLowerInvariant();
    }

    private static string GetField(Dictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var val) ? val : string.Empty;

    private static string ComputeDeterministicUid(string prefix, params string[] parts)
    {
        var joined = string.Join("|", parts.Select(p => p.Trim().ToLowerInvariant()));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return $"{prefix}:{Convert.ToHexString(hash[..8]).ToLowerInvariant()}";
    }
}
