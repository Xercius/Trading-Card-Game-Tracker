using System.Globalization;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Mtg;

/// <summary>
/// Importer and synchronizer for Magic: The Gathering using the Scryfall API.
/// Supports two-phase sync: metadata sync for sets, and selective card/printing sync by set.
/// </summary>
public sealed class MtgScryfallImporter : ISourceImporter
{
    public const string ImporterKey = "mtg";
    public string Key => ImporterKey;
    public string DisplayName => "Magic: The Gathering (Scryfall)";
    public IEnumerable<string> SupportedGames => new[] { MtgCatalog.GameName };

    private static readonly HashSet<string> AllowedImageHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cards.scryfall.io",
        "c1.scryfall.com",
        "c2.scryfall.com",
        "svgs.scryfall.io"
    };

    private readonly AppDbContext _db;
    private readonly IMtgApiClient _api;
    private readonly ILogger<MtgScryfallImporter> _logger;
    private readonly TimeProvider _timeProvider;

    public MtgScryfallImporter(
        AppDbContext db,
        IMtgApiClient api,
        ILogger<MtgScryfallImporter> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _api = api;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Phase 1: Fetches all non-digital sets from Scryfall and persists them in the <see cref="Set"/> table.
    /// </summary>
    public async Task<ImportSummary> SyncSetsAsync(CancellationToken ct = default)
    {
        var summary = new ImportSummary { Source = Key, DryRun = false };
        var catalog = new MtgCatalog(_db);
        await catalog.InitializeAsync(ct);

        var scryfallSets = await _api.GetSetsAsync(ct);
        var physicalSets = scryfallSets.Where(s => !s.Digital && !string.IsNullOrWhiteSpace(s.Code)).ToList();
        if (physicalSets.Count == 0)
        {
            summary.Messages.Add("No physical sets returned by Scryfall.");
            return summary;
        }

        var existingSets = await _db.Sets
            .Where(s => s.GameId == catalog.Game.Id)
            .ToDictionaryAsync(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

        var now = _timeProvider.GetUtcNow();
        foreach (var s in physicalSets)
        {
            var code = s.Code.Trim().ToUpperInvariant();
            var era = MtgCatalog.FormatSetTypeEra(s.SetType);
            var logoUrl = ImportHttpGuard.SanitizeAllowedImageUrl(s.IconSvgUri, AllowedImageHosts);
            DateOnly? releasedAt = DateOnly.TryParse(s.ReleasedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;

            if (existingSets.TryGetValue(code, out var existing))
            {
                existing.Name = s.Name.Trim();
                existing.Era = era;
                if (releasedAt.HasValue) existing.ReleasedAt = releasedAt;
                if (!string.IsNullOrWhiteSpace(logoUrl)) existing.LogoUrl = logoUrl;
                existing.LastSyncedAt = now;
            }
            else
            {
                var created = new Set
                {
                    GameId = catalog.Game.Id,
                    Code = code,
                    Name = s.Name.Trim(),
                    Era = era,
                    LogoUrl = logoUrl,
                    ReleasedAt = releasedAt,
                    LastSyncedAt = now
                };
                _db.Sets.Add(created);
                existingSets[code] = created;
            }
        }

        await UpsertHistoryCheckpointAsync("mtg-sets", string.Empty, now, ct);
        await _db.SaveChangesAsync(ct);

        summary.Messages.Add($"Successfully synced {physicalSets.Count} MTG sets.");
        _logger.LogInformation("Completed MTG set sync. Total physical sets: {Count}", physicalSets.Count);
        return summary;
    }

    /// <summary>
    /// Phase 2: Imports English cards and physical finish printings for the specified set codes.
    /// </summary>
    public async Task<ImportSummary> SyncCardsAsync(
        IReadOnlyList<string> setCodes,
        bool forceFullSync = false,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var summary = new ImportSummary { Source = Key, DryRun = dryRun };
        if (setCodes is null || setCodes.Count == 0)
        {
            summary.Messages.Add("No set codes provided for card sync.");
            return summary;
        }

        var catalog = new MtgCatalog(_db);
        await catalog.InitializeAsync(ct);

        return await _db.WithDryRunAsync(dryRun, async () =>
        {
            var dbSets = await _db.Sets
                .Where(s => s.GameId == catalog.Game.Id)
                .ToDictionaryAsync(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

            var existingCards = await _db.Cards
                .Include(c => c.CardAspects)
                .Include(c => c.CardTypes)
                .Include(c => c.CardSubTypes)
                .Include(c => c.CardKeywords)
                .Include(c => c.Printings).ThenInclude(p => p.Treatment)
                .Where(c => c.GameId == catalog.Game.Id)
                .AsSplitQuery()
                .ToListAsync(ct);

            var cardsByUid = existingCards
                .Where(c => !string.IsNullOrWhiteSpace(c.CardUid))
                .GroupBy(c => c.CardUid!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var printingsByUid = existingCards
                .SelectMany(c => c.Printings)
                .Where(p => !string.IsNullOrWhiteSpace(p.CardUid))
                .GroupBy(p => p.CardUid!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var rawCode in setCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var code = rawCode.Trim().ToUpperInvariant();
                if (!dbSets.TryGetValue(code, out var dbSet))
                {
                    summary.Errors++;
                    summary.Messages.Add($"Set '{code}' was not found in the database. Run 'Sync Sets' first.");
                    continue;
                }

                await SyncSingleSetAsync(dbSet, catalog, cardsByUid, printingsByUid, forceFullSync, dryRun, summary, ct);
            }

            return summary;
        });
    }

    /// <inheritdoc />
    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.SetCode))
        {
            throw new ArgumentException("SetCode is required for MTG Scryfall imports.", nameof(options));
        }

        return await SyncCardsAsync([options.SetCode.Trim()], forceFullSync: true, dryRun: options.DryRun, ct);
    }

    /// <inheritdoc />
    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
        => throw new NotSupportedException("MTG file import is not supported. Use SyncSetsAsync and SyncCardsAsync.");

    private async Task SyncSingleSetAsync(
        Set dbSet,
        MtgCatalog catalog,
        Dictionary<string, Card> cardsByUid,
        Dictionary<string, CardPrinting> printingsByUid,
        bool forceFullSync,
        bool dryRun,
        ImportSummary summary,
        CancellationToken ct)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var syncLog = new SyncLog
        {
            SetId = dbSet.Id,
            CatalogScope = $"Mtg:{dbSet.Code}",
            StartedAt = startedAt,
            IsIncremental = !forceFullSync,
            Status = "Pending"
        };
        if (!dryRun)
        {
            _db.SyncLogs.Add(syncLog);
            await _db.SaveChangesAsync(ct);
        }

        try
        {
            var scryfallCards = await _api.GetSetCardsAsync(dbSet.Code, ct);
            syncLog.CardsReturned = scryfallCards.Count;

            var upserted = 0;
            foreach (var sc in scryfallCards)
            {
                if (string.IsNullOrWhiteSpace(sc.CollectorNumber))
                {
                    summary.Errors++;
                    summary.Messages.Add($"Skipped '{sc.Name}' ({sc.Id}): missing collector_number.");
                    continue;
                }

                var card = UpsertLogicalCard(sc, catalog, cardsByUid, summary);
                upserted += UpsertCardPrintings(sc, card, dbSet, catalog, printingsByUid, summary);
            }

            var completedAt = _timeProvider.GetUtcNow();
            if (!dryRun)
            {
                await UpsertHistoryCheckpointAsync("mtg-cards", dbSet.Code, completedAt, ct);
                syncLog.Status = "Succeeded";
                syncLog.CompletedAt = completedAt;
                syncLog.CardsUpserted = upserted;
            }

            await _db.SaveChangesAsync(ct);
            summary.Messages.Add($"Synced {scryfallCards.Count} cards for set '{dbSet.Code}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to sync MTG cards for set {SetCode}.", dbSet.Code);
            summary.Errors++;
            summary.Messages.Add($"Failed to sync set '{dbSet.Code}': {ex.Message}");
            if (!dryRun)
            {
                syncLog.Status = "Failed";
                syncLog.CompletedAt = _timeProvider.GetUtcNow();
                syncLog.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private Card UpsertLogicalCard(
        ScryfallCard sc,
        MtgCatalog catalog,
        Dictionary<string, Card> cardsByUid,
        ImportSummary summary)
    {
        var uid = MtgCatalog.ComputeCanonicalCardUid(sc);
        var rulesText = MtgCatalog.BuildRulesText(sc);
        var extraJson = MtgCatalog.BuildExtraCardDataJson(sc);
        var backFace = sc.CardFaces?.ElementAtOrDefault(1);
        int? cmc = sc.Cmc.HasValue ? (int)Math.Round(sc.Cmc.Value, MidpointRounding.AwayFromZero) : null;
        var now = _timeProvider.GetUtcNow();

        if (!cardsByUid.TryGetValue(uid, out var card))
        {
            card = new Card
            {
                GameId = catalog.Game.Id,
                CardUid = uid,
                Name = sc.Name.Trim(),
                Cost = cmc,
                Text = rulesText,
                BackTitle = backFace?.Name?.Trim(),
                BackText = backFace?.OracleText?.Trim(),
                ExtraCardDataJson = extraJson,
                LastSyncedAt = now
            };
            _db.Cards.Add(card);
            cardsByUid[uid] = card;
            summary.CardsCreated++;
        }
        else
        {
            var changed = card.Name != sc.Name.Trim()
                || card.Cost != cmc
                || card.Text != rulesText
                || card.BackTitle != backFace?.Name?.Trim()
                || card.BackText != backFace?.OracleText?.Trim()
                || card.ExtraCardDataJson != extraJson;

            if (changed)
            {
                card.Name = sc.Name.Trim();
                card.Cost = cmc;
                card.Text = rulesText;
                card.BackTitle = backFace?.Name?.Trim();
                card.BackText = backFace?.OracleText?.Trim();
                card.ExtraCardDataJson = extraJson;
                card.LastSyncedAt = now;
                summary.CardsUpdated++;
            }
        }

        SyncCardMetadataJoins(card, sc, catalog);
        return card;
    }

    private static void SyncCardMetadataJoins(Card card, ScryfallCard sc, MtgCatalog catalog)
    {
        var desiredAspectIds = catalog.ResolveAspects(sc).Select(a => a.Id).ToHashSet();
        var currentAspectIds = card.CardAspects.Select(ca => ca.AspectId).ToHashSet();
        if (!desiredAspectIds.SetEquals(currentAspectIds))
        {
            card.CardAspects.Clear();
            foreach (var aspect in catalog.ResolveAspects(sc))
            {
                card.CardAspects.Add(new CardAspect { Card = card, Aspect = aspect });
            }
        }

        var (types, subTypes) = MtgCatalog.ParseTypeLine(sc.TypeLine);
        var desiredTypes = types.Select(catalog.GetOrCreateCardType).ToList();
        if (!desiredTypes.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(card.CardTypes.Select(ct => ct.CardType?.Name ?? string.Empty)))
        {
            card.CardTypes.Clear();
            foreach (var tp in desiredTypes)
            {
                card.CardTypes.Add(new CardCardType { Card = card, CardType = tp, SourceRole = "Primary" });
            }
        }

        var desiredSubTypes = subTypes.Select(catalog.GetOrCreateSubType).ToList();
        if (!desiredSubTypes.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(card.CardSubTypes.Select(cs => cs.SubType?.Name ?? string.Empty)))
        {
            card.CardSubTypes.Clear();
            foreach (var st in desiredSubTypes)
            {
                card.CardSubTypes.Add(new CardSubType { Card = card, SubType = st });
            }
        }

        var desiredKeywords = (sc.Keywords ?? []).Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(catalog.GetOrCreateKeyword)
            .ToList();
        if (!desiredKeywords.Select(k => k.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(card.CardKeywords.Select(ck => ck.Keyword?.Name ?? string.Empty)))
        {
            card.CardKeywords.Clear();
            foreach (var kw in desiredKeywords)
            {
                card.CardKeywords.Add(new CardKeyword { Card = card, Keyword = kw });
            }
        }
    }

    private int UpsertCardPrintings(
        ScryfallCard sc,
        Card card,
        Set dbSet,
        MtgCatalog catalog,
        Dictionary<string, CardPrinting> printingsByUid,
        ImportSummary summary)
    {
        var finishes = sc.Finishes is { Count: > 0 } ? sc.Finishes : new[] { "nonfoil" };
        var frontRaw = sc.ImageUris?.Normal ?? sc.CardFaces?.ElementAtOrDefault(0)?.ImageUris?.Normal;
        var backRaw = sc.CardFaces?.ElementAtOrDefault(1)?.ImageUris?.Normal;
        var frontImage = ImportHttpGuard.SanitizeAllowedImageUrl(frontRaw, AllowedImageHosts);
        var backImage = ImportHttpGuard.SanitizeAllowedImageUrl(backRaw, AllowedImageHosts);
        var rarity = catalog.GetOrCreateRarity(sc.Rarity);
        var number = sc.CollectorNumber!.Trim();
        var now = _timeProvider.GetUtcNow();
        var upserted = 0;

        foreach (var finish in finishes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var printingUid = $"{sc.Id.Trim()}|{finish.Trim().ToLowerInvariant()}";
            var treatmentName = MtgCatalog.ResolveTreatmentName(sc, finish);
            var treatment = catalog.GetOrCreateTreatment(treatmentName);
            var isFoil = finish.Equals("foil", StringComparison.OrdinalIgnoreCase)
                      || finish.Equals("etched", StringComparison.OrdinalIgnoreCase);
            var detailsJson = MtgCatalog.BuildPrintingDetailsJson(sc, finish, treatmentName);

            if (!printingsByUid.TryGetValue(printingUid, out var printing))
            {
                printing = new CardPrinting
                {
                    Card = card,
                    CatalogSet = dbSet,
                    Number = number,
                    CardUid = printingUid,
                    RarityLookup = rarity,
                    Treatment = treatment,
                    IsFoil = isFoil,
                    ImageUrl = frontImage,
                    BackImageUrl = backImage,
                    Artist = sc.Artist?.Trim(),
                    DetailsJson = detailsJson,
                    LastSyncedAt = now
                };
                _db.CardPrintings.Add(printing);
                printingsByUid[printingUid] = printing;
                summary.PrintingsCreated++;
                upserted++;
            }
            else
            {
                var changed = printing.Card != card
                    || printing.Number != number
                    || printing.RarityLookup != rarity
                    || printing.Treatment != treatment
                    || printing.IsFoil != isFoil
                    || printing.ImageUrl != frontImage
                    || printing.BackImageUrl != backImage
                    || printing.DetailsJson != detailsJson;

                if (changed)
                {
                    printing.Card = card;
                    printing.CatalogSet = dbSet;
                    printing.Number = number;
                    printing.RarityLookup = rarity;
                    printing.Treatment = treatment;
                    printing.IsFoil = isFoil;
                    printing.ImageUrl = frontImage;
                    printing.BackImageUrl = backImage;
                    printing.Artist = sc.Artist?.Trim();
                    printing.DetailsJson = detailsJson;
                    printing.LastSyncedAt = now;
                    summary.PrintingsUpdated++;
                    upserted++;
                }
            }
        }

        return upserted;
    }

    private async Task UpsertHistoryCheckpointAsync(string importerKey, string setCode, DateTimeOffset syncedAt, CancellationToken ct)
    {
        var entry = await _db.ImportSyncHistories
            .FirstOrDefaultAsync(h => h.ImporterKey == importerKey && h.SetCode == setCode, ct);

        if (entry is null)
        {
            _db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = importerKey,
                SetCode = setCode,
                LastSyncedAt = syncedAt
            });
        }
        else
        {
            entry.LastSyncedAt = syncedAt;
        }
    }
}
