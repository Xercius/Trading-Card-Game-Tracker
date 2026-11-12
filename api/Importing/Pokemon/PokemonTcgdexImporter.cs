using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Importing.Pokemon;

/// <summary>
/// Importer and synchronizer for Pokémon TCG using the TCGdex API.
/// Supports two-phase sync: metadata sync for sets/eras, and selective card sync by set.
/// </summary>
public sealed class PokemonTcgdexImporter : ISourceImporter
{
    public const string ImporterKey = "pokemon";
    public string Key => ImporterKey;
    public string DisplayName => "Pokémon TCG (TCGdex)";
    public IEnumerable<string> SupportedGames => new[] { "Pokemon TCG" };

    private readonly AppDbContext _db;
    private readonly IPokemonApiClient _api;
    private readonly ILogger<PokemonTcgdexImporter> _logger;
    private readonly TimeProvider _timeProvider;

    public PokemonTcgdexImporter(
        AppDbContext db,
        IPokemonApiClient api,
        ILogger<PokemonTcgdexImporter> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _api = api;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Phase 1: Fetches all sets from TCGdex and persists them in the Set table with their era and release date.
    /// </summary>
    public async Task<ImportSummary> SyncSetsAsync(CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = false
        };

        var catalog = new PokemonCatalog(_db);
        await catalog.InitializeAsync(ct);
        var game = catalog.Game;

        _logger.LogInformation("Starting Pokemon set sync from TCGdex...");
        var setResumes = await _api.GetSetsAsync(ct);
        if (setResumes.Count == 0)
        {
            summary.Messages.Add("No sets returned by TCGdex.");
            return summary;
        }

        var existingSets = await _db.Sets
            .Where(s => s.GameId == game.Id)
            .ToDictionaryAsync(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

        // Fetch details in throttled batches to populate Era and ReleaseDate
        var semaphore = new SemaphoreSlim(8);
        var tasks = setResumes.Select(async resume =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var code = resume.Id.Trim();
                existingSets.TryGetValue(code, out var existing);

                // Fetch detail if set does not exist or lacks Era/ReleaseDate/LogoUrl
                TcgdexSetDetail? detail = null;
                if (existing == null ||
                    string.IsNullOrWhiteSpace(existing.Era) ||
                    existing.ReleasedAt == null ||
                    (string.IsNullOrWhiteSpace(existing.LogoUrl) && string.IsNullOrWhiteSpace(resume.Logo)))
                {
                    detail = await _api.GetSetAsync(code, ct);
                }

                return (Resume: resume, Detail: detail, Existing: existing);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (resume, detail, existing) in results)
        {
            var code = resume.Id.Trim();
            var name = !string.IsNullOrWhiteSpace(detail?.Name) ? detail.Name.Trim() : resume.Name.Trim();
            var era = detail?.Serie?.Name?.Trim() ?? "Other";
            var logoUrl = PokemonCatalog.NormalizeSetLogoUrl(detail?.Logo ?? resume.Logo);
            DateOnly? releasedAt = null;
            var dateStr = detail?.ReleaseDate;
            if (!string.IsNullOrWhiteSpace(dateStr) && DateOnly.TryParse(dateStr, out var d))
            {
                releasedAt = d;
            }

            if (existing != null)
            {
                existing.Name = name;
                if (!string.IsNullOrWhiteSpace(era)) existing.Era = era;
                if (releasedAt.HasValue) existing.ReleasedAt = releasedAt;
                if (!string.IsNullOrWhiteSpace(logoUrl)) existing.LogoUrl = logoUrl;
                existing.LastSyncedAt = _timeProvider.GetUtcNow();
            }
            else
            {
                var newSet = new Set
                {
                    GameId = game.Id,
                    Code = code,
                    Name = name,
                    Era = era,
                    LogoUrl = logoUrl,
                    ReleasedAt = releasedAt,
                    LastSyncedAt = _timeProvider.GetUtcNow()
                };
                _db.Sets.Add(newSet);
                existingSets[code] = newSet;
                catalog.RegisterSet(newSet);
            }
        }

        // Record sets sync checkpoint
        var setsCheckpoint = await _db.ImportSyncHistories
            .FirstOrDefaultAsync(h => h.ImporterKey == "pokemon-sets" && h.SetCode == string.Empty, ct);

        if (setsCheckpoint == null)
        {
            _db.ImportSyncHistories.Add(new ImportSyncHistory
            {
                ImporterKey = "pokemon-sets",
                SetCode = string.Empty,
                LastSyncedAt = _timeProvider.GetUtcNow()
            });
        }
        else
        {
            setsCheckpoint.LastSyncedAt = _timeProvider.GetUtcNow();
        }

        var consolidatedCount = await ConsolidateDuplicateCardsCoreAsync(game, ct);
        if (consolidatedCount > 0)
        {
            summary.Messages.Add($"Consolidated {consolidatedCount} duplicate Pokemon cards into canonical card records.");
        }

        await _db.SaveChangesAsync(ct);
        summary.Messages.Add($"Successfully synced {results.Length} Pokemon sets.");
        _logger.LogInformation("Completed Pokemon set sync. Total sets: {Count}", results.Length);
        return summary;
    }

    /// <summary>
    /// Phase 2: Imports cards and variants for the specified set codes.
    /// </summary>
    public async Task<ImportSummary> SyncCardsAsync(
        IReadOnlyList<string> setCodes,
        bool forceFullSync = false,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var summary = new ImportSummary
        {
            Source = Key,
            DryRun = dryRun
        };

        if (setCodes == null || setCodes.Count == 0)
        {
            summary.Messages.Add("No set codes provided for card sync.");
            return summary;
        }

        var catalog = new PokemonCatalog(_db);
        await catalog.InitializeAsync(ct);
        var game = catalog.Game;

        return await _db.WithDryRunAsync(dryRun, async () =>
        {
            var consolidatedCount = await ConsolidateDuplicateCardsCoreAsync(game, ct);
            if (consolidatedCount > 0)
            {
                await _db.SaveChangesAsync(ct);
                summary.Messages.Add($"Consolidated {consolidatedCount} duplicate Pokemon cards into canonical card records.");
            }

            var syncHistories = await _db.ImportSyncHistories
                .Where(h => h.ImporterKey == "pokemon-cards")
                .ToDictionaryAsync(h => h.SetCode.Trim(), StringComparer.OrdinalIgnoreCase, ct);

            var dbSets = await _db.Sets
                .Where(s => s.GameId == game.Id)
                .ToDictionaryAsync(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

            // Load all existing Pokemon cards across the entire game keyed by canonical CardUid
            var existingCards = await _db.Cards
                .Include(c => c.CardAspects)
                .Include(c => c.CardTypes)
                .Include(c => c.CardSubTypes)
                .Include(c => c.Printings).ThenInclude(p => p.Treatment)
                .Where(c => c.GameId == game.Id)
                .AsSplitQuery()
                .ToListAsync(ct);
            var existingCardsByUid = existingCards
                .Where(c => !string.IsNullOrWhiteSpace(c.CardUid))
                .GroupBy(c => c.CardUid!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var existingPrintings = existingCards.SelectMany(c => c.Printings).ToList();

            var cardFetchSemaphore = new SemaphoreSlim(6);

            foreach (var setCode in setCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var trimmedCode = setCode.Trim();

                if (!dbSets.TryGetValue(trimmedCode, out var dbSet))
                {
                    summary.Errors++;
                    summary.Messages.Add($"Set with code '{trimmedCode}' was not found in the database. Run 'Sync Sets' first.");
                    continue;
                }

                _logger.LogInformation("Fetching cards for set '{SetCode}' ({SetName})...", dbSet.Code, dbSet.Name);
                var setDetail = await _api.GetSetAsync(trimmedCode, ct);
                var normalizedLogoUrl = PokemonCatalog.NormalizeSetLogoUrl(setDetail?.Logo);
                if (!string.IsNullOrWhiteSpace(normalizedLogoUrl))
                {
                    dbSet.LogoUrl = normalizedLogoUrl;
                }
                if (setDetail?.Cards == null || setDetail.Cards.Count == 0)
                {
                    summary.Messages.Add($"Set '{trimmedCode}' has no cards in TCGdex.");
                    continue;
                }

                syncHistories.TryGetValue(trimmedCode, out var lastHistory);
                var lowerBoundTime = forceFullSync ? DateTimeOffset.MinValue : (lastHistory?.LastSyncedAt ?? DateTimeOffset.MinValue);

                // Concurrently fetch full card details
                var fetchTasks = setDetail.Cards.Select(async stub =>
                {
                    await cardFetchSemaphore.WaitAsync(ct);
                    try
                    {
                        return await _api.GetCardAsync(stub.Id, ct);
                    }
                    finally
                    {
                        cardFetchSemaphore.Release();
                    }
                });

                var cardsFetched = await Task.WhenAll(fetchTasks);

                int setCardsUpserted = 0;
                int setPrintingsUpserted = 0;
                var cardsUpsertedInSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cardDetail in cardsFetched)
                {
                    if (cardDetail == null) continue;

                    DateTimeOffset cardUpdated = DateTimeOffset.MinValue;
                    if (!string.IsNullOrWhiteSpace(cardDetail.Updated) && DateTimeOffset.TryParse(cardDetail.Updated, out var u))
                    {
                        cardUpdated = u;
                    }

                    var localId = cardDetail.LocalId.Trim();
                    var canonicalCardUid = catalog.ComputeMechanicalCardUid(cardDetail);
                    existingCardsByUid.TryGetValue(canonicalCardUid, out var existingCard);
                    var normalizedStage = PokemonCatalog.NormalizeStage(cardDetail.Stage);
                    var rulesText = catalog.BuildRulesText(cardDetail);
                    var variantPlans = DetermineVariants(cardDetail)
                        .Select(variant =>
                        {
                            var printingUid = BuildPrintingUid(cardDetail.Id, variant.TreatmentName, variant.Detail);
                            var printing = existingPrintings.FirstOrDefault(p =>
                                p.SetId == dbSet.Id &&
                                string.Equals(p.Number, localId, StringComparison.OrdinalIgnoreCase) &&
                                IsPrintingIdentityMatch(p, cardDetail.Id, printingUid, variant.TreatmentName, variant.Detail));
                            return (Variant: variant, PrintingUid: printingUid, ExistingPrinting: printing);
                        })
                        .ToList();
                    existingCard = variantPlans
                        .Select(plan => plan.ExistingPrinting?.Card)
                        .FirstOrDefault(card => card != null)
                        ?? existingCard;

                    // Check if this specific collector number in this set is already present and unchanged
                    if (!forceFullSync &&
                        existingCard != null &&
                        string.Equals(existingCard.CardUid, canonicalCardUid, StringComparison.Ordinal) &&
                        variantPlans.Count > 0 &&
                        variantPlans.All(plan =>
                            plan.ExistingPrinting is { } printing &&
                            string.Equals(printing.CardUid, plan.PrintingUid, StringComparison.Ordinal) &&
                            string.Equals(printing.Treatment?.Name, plan.Variant.TreatmentName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(printing.DetailsJson, catalog.BuildPrintingDetailsJson(cardDetail, plan.Variant.Detail), StringComparison.Ordinal)) &&
                        cardUpdated != DateTimeOffset.MinValue &&
                        cardUpdated <= lowerBoundTime)
                    {
                        continue; // Skip unchanged printing
                    }

                    // 1. Upsert Card (shared across all printings/treatments of this mechanical card)
                    if (existingCard == null)
                    {
                        existingCard = new Card
                        {
                            GameId = game.Id,
                            CardUid = canonicalCardUid,
                            Name = cardDetail.Name.Trim(),
                            Hp = cardDetail.Hp,
                            Stage = normalizedStage,
                            Text = rulesText,
                            Rules = null,
                            Lore = cardDetail.Description,
                            ExtraCardDataJson = catalog.BuildExtraCardDataJson(cardDetail),
                            LastSyncedAt = _timeProvider.GetUtcNow()
                        };
                        _db.Cards.Add(existingCard);
                        cardsUpsertedInSet.Add(canonicalCardUid);
                        summary.CardsCreated++;
                        setCardsUpserted++;
                    }
                    else
                    {
                        existingCard.CardUid = canonicalCardUid;
                        existingCard.Name = cardDetail.Name.Trim();
                        existingCard.Hp = cardDetail.Hp;
                        existingCard.Stage = normalizedStage;
                        existingCard.Text = rulesText;
                        existingCard.Rules = null;
                        if (!string.IsNullOrWhiteSpace(cardDetail.Description))
                        {
                            existingCard.Lore = cardDetail.Description;
                        }
                        existingCard.ExtraCardDataJson = catalog.BuildExtraCardDataJson(cardDetail);
                        existingCard.LastSyncedAt = _timeProvider.GetUtcNow();

                        if (cardsUpsertedInSet.Add(canonicalCardUid))
                        {
                            summary.CardsUpdated++;
                            setCardsUpserted++;
                        }
                    }
                    existingCardsByUid[canonicalCardUid] = existingCard;

                    // 2. Link Aspects, CardTypes & SubTypes without duplicating tracked join entities
                    SyncCardLinks(existingCard, cardDetail, catalog);

                    // 3. Upsert Printings for each physical variant of this collector number
                    var rarity = catalog.GetOrCreateRarity(cardDetail.Rarity);
                    var imageUrl = !string.IsNullOrWhiteSpace(cardDetail.Image) ? $"{cardDetail.Image}/high.webp" : null;

                    foreach (var plan in variantPlans)
                    {
                        var (treatmentName, isFoil, variantDetail) = plan.Variant;
                        var treatment = catalog.GetOrCreateTreatment(treatmentName);
                        var printingUid = plan.PrintingUid;
                        var detailsJson = catalog.BuildPrintingDetailsJson(cardDetail, variantDetail);

                        var printing = plan.ExistingPrinting;

                        if (printing == null)
                        {
                            printing = new CardPrinting
                            {
                                SetId = dbSet.Id,
                                Card = existingCard,
                                Number = localId,
                                CardUid = printingUid,
                                ImageUrl = imageUrl,
                                Artist = cardDetail.Illustrator?.Trim(),
                                Treatment = treatment,
                                RarityLookup = rarity,
                                IsFoil = isFoil,
                                DetailsJson = detailsJson,
                                ApiUpdatedAt = cardUpdated != DateTimeOffset.MinValue ? cardUpdated : null
                            };
                            existingCard.Printings.Add(printing);
                            _db.CardPrintings.Add(printing);
                            existingPrintings.Add(printing);
                            summary.PrintingsCreated++;
                        }
                        else
                        {
                            if (!ReferenceEquals(printing.Card, existingCard))
                            {
                                printing.Card?.Printings.Remove(printing);
                                printing.Card = existingCard;
                                printing.CardId = existingCard.Id;
                                existingCard.Printings.Add(printing);
                            }
                            printing.CardUid = printingUid;
                            printing.Number = localId;
                            printing.ImageUrl = imageUrl;
                            printing.Artist = cardDetail.Illustrator?.Trim();
                            printing.Treatment = treatment;
                            printing.RarityLookup = rarity;
                            printing.IsFoil = isFoil;
                            printing.DetailsJson = detailsJson;
                            if (cardUpdated != DateTimeOffset.MinValue)
                            {
                                printing.ApiUpdatedAt = cardUpdated;
                            }
                            summary.PrintingsUpdated++;
                        }
                        setPrintingsUpserted++;
                    }
                }

                // Update set sync history checkpoint
                if (lastHistory == null)
                {
                    lastHistory = new ImportSyncHistory
                    {
                        ImporterKey = "pokemon-cards",
                        SetCode = trimmedCode,
                        LastSyncedAt = _timeProvider.GetUtcNow()
                    };
                    _db.ImportSyncHistories.Add(lastHistory);
                    syncHistories[trimmedCode] = lastHistory;
                }
                else
                {
                    lastHistory.LastSyncedAt = _timeProvider.GetUtcNow();
                }

                // Record sync log
                _db.SyncLogs.Add(new SyncLog
                {
                    SetId = dbSet.Id,
                    CatalogScope = $"Pokemon:{trimmedCode}",
                    StartedAt = _timeProvider.GetUtcNow(),
                    CompletedAt = _timeProvider.GetUtcNow(),
                    CardsReturned = cardsFetched.Length,
                    CardsUpserted = setCardsUpserted,
                    IsIncremental = !forceFullSync && lowerBoundTime > DateTimeOffset.MinValue,
                    Status = "Succeeded"
                });

                await _db.SaveChangesAsync(ct);
                summary.Messages.Add($"Set '{trimmedCode}': {setCardsUpserted} cards upserted, {setPrintingsUpserted} printings upserted.");
            }

            var postSyncConsolidatedCount = await ConsolidateDuplicateCardsCoreAsync(game, ct);
            if (postSyncConsolidatedCount > 0)
            {
                await _db.SaveChangesAsync(ct);
                summary.Messages.Add($"Consolidated {postSyncConsolidatedCount} duplicate Pokemon cards into canonical card records.");
            }

            return summary;
        });
    }

    public async Task<ImportSummary> ImportFromRemoteAsync(ImportOptions options, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(options.SetCode))
        {
            return await SyncCardsAsync(new[] { options.SetCode }, forceFullSync: false, dryRun: options.DryRun, ct);
        }
        return await SyncSetsAsync(ct);
    }

    public Task<ImportSummary> ImportFromFileAsync(Stream file, ImportOptions options, CancellationToken ct = default)
    {
        throw new NotSupportedException("File-based sync is not supported for TCGdex. Use remote sync.");
    }

    private async Task<int> ConsolidateDuplicateCardsCoreAsync(
        Game game,
        CancellationToken ct)
    {
        var allPokemonCards = await _db.Cards
            .Include(c => c.CardAspects).ThenInclude(ca => ca.Aspect)
            .Include(c => c.CardTypes).ThenInclude(ct => ct.CardType)
            .Include(c => c.CardSubTypes).ThenInclude(cst => cst.SubType)
            .Include(c => c.Printings)
            .Where(c => c.GameId == game.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (allPokemonCards.Count == 0)
        {
            return 0;
        }

        var groups = allPokemonCards
            .GroupBy(PokemonCatalog.ComputeMechanicalCardUid, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int duplicatesRemoved = 0;

        foreach (var group in groups)
        {
            var canonicalUid = group.Key;
            var ordered = group
                .OrderByDescending(c => string.Equals(c.CardUid?.Trim(), canonicalUid, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.Printings.Any(p => !string.IsNullOrWhiteSpace(p.ImageUrl)))
                .ThenBy(c => c.Id)
                .ToList();

            var canonicalCard = ordered[0];
            if (!string.Equals(canonicalCard.CardUid, canonicalUid, StringComparison.Ordinal))
            {
                canonicalCard.CardUid = canonicalUid;
            }

            if (ordered.Count <= 1)
            {
                continue;
            }

            foreach (var dupCard in ordered.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(canonicalCard.Lore) && !string.IsNullOrWhiteSpace(dupCard.Lore))
                {
                    canonicalCard.Lore = dupCard.Lore;
                }

                if (string.IsNullOrWhiteSpace(canonicalCard.ExtraCardDataJson) && !string.IsNullOrWhiteSpace(dupCard.ExtraCardDataJson))
                {
                    canonicalCard.ExtraCardDataJson = dupCard.ExtraCardDataJson;
                }

                // Merge any missing aspects/types/subtypes from duplicate card
                foreach (var aspectLink in dupCard.CardAspects)
                {
                    if (!canonicalCard.CardAspects.Any(a => a.AspectId == aspectLink.AspectId))
                    {
                        canonicalCard.CardAspects.Add(new CardAspect
                        {
                            Card = canonicalCard,
                            AspectId = aspectLink.AspectId,
                            Aspect = aspectLink.Aspect,
                            Count = aspectLink.Count
                        });
                    }
                }

                foreach (var typeLink in dupCard.CardTypes)
                {
                    if (!canonicalCard.CardTypes.Any(t => t.CardTypeId == typeLink.CardTypeId && t.SourceRole == typeLink.SourceRole))
                    {
                        canonicalCard.CardTypes.Add(new CardCardType
                        {
                            Card = canonicalCard,
                            CardTypeId = typeLink.CardTypeId,
                            CardType = typeLink.CardType,
                            SourceRole = typeLink.SourceRole
                        });
                    }
                }

                foreach (var subTypeLink in dupCard.CardSubTypes)
                {
                    if (!canonicalCard.CardSubTypes.Any(st => st.SubTypeId == subTypeLink.SubTypeId))
                    {
                        canonicalCard.CardSubTypes.Add(new CardSubType
                        {
                            Card = canonicalCard,
                            SubTypeId = subTypeLink.SubTypeId,
                            SubType = subTypeLink.SubType
                        });
                    }
                }

                // Re-parent printings to canonicalCard so all CardPrinting.Id references in UserCards/Decks/Wishlists stay intact
                foreach (var printing in dupCard.Printings.ToList())
                {
                    var conflictingPrinting = canonicalCard.Printings.FirstOrDefault(p =>
                        p.SetId == printing.SetId &&
                        string.Equals(p.Number, printing.Number, StringComparison.OrdinalIgnoreCase) &&
                        p.TreatmentId == printing.TreatmentId);

                    if (conflictingPrinting == null)
                    {
                        dupCard.Printings.Remove(printing);
                        printing.CardId = canonicalCard.Id;
                        printing.Card = canonicalCard;
                        canonicalCard.Printings.Add(printing);
                    }
                    else
                    {
                        await MergeConflictingPrintingReferencesAsync(printing.Id, conflictingPrinting.Id, ct);
                        dupCard.Printings.Remove(printing);
                        _db.CardPrintings.Remove(printing);
                    }
                }

                _db.Cards.Remove(dupCard);
                duplicatesRemoved++;
            }
        }

        return duplicatesRemoved;
    }

    private async Task MergeConflictingPrintingReferencesAsync(
        int duplicatePrintingId,
        int canonicalPrintingId,
        CancellationToken ct)
    {
        if (duplicatePrintingId == 0 || duplicatePrintingId == canonicalPrintingId)
        {
            return;
        }

        var userCards = await _db.UserCards
            .Where(uc => uc.CardPrintingId == duplicatePrintingId)
            .ToListAsync(ct);
        foreach (var uc in userCards)
        {
            var existingTarget = _db.UserCards.Local.FirstOrDefault(
                x => !ReferenceEquals(x, uc) && x.UserId == uc.UserId && x.CardPrintingId == canonicalPrintingId)
                ?? await _db.UserCards.FirstOrDefaultAsync(
                    x => x.UserId == uc.UserId && x.CardPrintingId == canonicalPrintingId, ct);
            if (existingTarget != null)
            {
                existingTarget.QuantityOwned += uc.QuantityOwned;
                existingTarget.QuantityWanted += uc.QuantityWanted;
                existingTarget.QuantityProxyOwned += uc.QuantityProxyOwned;
            }
            else
            {
                _db.UserCards.Add(new UserCard
                {
                    UserId = uc.UserId,
                    CardPrintingId = canonicalPrintingId,
                    QuantityOwned = uc.QuantityOwned,
                    QuantityWanted = uc.QuantityWanted,
                    QuantityProxyOwned = uc.QuantityProxyOwned
                });
            }
            _db.UserCards.Remove(uc);
        }

        var deckCards = await _db.DeckCards
            .Where(dc => dc.CardPrintingId == duplicatePrintingId)
            .ToListAsync(ct);
        foreach (var dc in deckCards)
        {
            var existingTarget = _db.DeckCards.Local.FirstOrDefault(
                x => !ReferenceEquals(x, dc) && x.DeckId == dc.DeckId && x.CardPrintingId == canonicalPrintingId)
                ?? await _db.DeckCards.FirstOrDefaultAsync(
                    x => x.DeckId == dc.DeckId && x.CardPrintingId == canonicalPrintingId, ct);
            if (existingTarget != null)
            {
                existingTarget.QuantityInDeck += dc.QuantityInDeck;
                existingTarget.QuantityIdea += dc.QuantityIdea;
                existingTarget.QuantityAcquire += dc.QuantityAcquire;
                existingTarget.QuantityProxy += dc.QuantityProxy;
                existingTarget.IsStarting |= dc.IsStarting;
                _db.DeckCards.Remove(dc);
            }
            else
            {
                dc.CardPrintingId = canonicalPrintingId;
            }
        }

        var priceHistories = await _db.CardPriceHistories
            .Where(p => p.CardPrintingId == duplicatePrintingId)
            .ToListAsync(ct);
        foreach (var ph in priceHistories)
        {
            var exists = _db.CardPriceHistories.Local.Any(
                    x => !ReferenceEquals(x, ph) && x.CardPrintingId == canonicalPrintingId && x.CapturedAt == ph.CapturedAt)
                || await _db.CardPriceHistories.AnyAsync(
                    x => x.CardPrintingId == canonicalPrintingId && x.CapturedAt == ph.CapturedAt, ct);
            if (exists)
            {
                _db.CardPriceHistories.Remove(ph);
            }
            else
            {
                ph.CardPrintingId = canonicalPrintingId;
            }
        }
    }

    private static void SyncCardLinks(Card card, TcgdexCard cardDetail, PokemonCatalog catalog)
    {
        // 1. Aspects (Energy Types)
        var desiredAspects = (cardDetail.Types ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => catalog.GetOrCreateAspect(t))
            .Distinct()
            .ToList();

        foreach (var existing in card.CardAspects.ToList())
        {
            if (!desiredAspects.Any(a => a == existing.Aspect || (a.Id != 0 && a.Id == existing.AspectId)))
            {
                card.CardAspects.Remove(existing);
            }
        }

        foreach (var aspect in desiredAspects)
        {
            if (!card.CardAspects.Any(a => a.Aspect == aspect || (aspect.Id != 0 && a.AspectId == aspect.Id)))
            {
                card.CardAspects.Add(new CardAspect
                {
                    Card = card,
                    Aspect = aspect,
                    Count = 1
                });
            }
        }

        // 2. CardTypes
        var primaryType = PokemonCatalog.ResolvePrimaryCardType(cardDetail.Category, cardDetail.TrainerType);
        var catType = catalog.GetOrCreateCardType(primaryType);

        foreach (var existing in card.CardTypes.ToList())
        {
            if (!(existing.SourceRole == "Type" && (existing.CardType == catType || (catType.Id != 0 && existing.CardTypeId == catType.Id))))
            {
                card.CardTypes.Remove(existing);
            }
        }

        if (!card.CardTypes.Any(t => t.SourceRole == "Type" && (t.CardType == catType || (catType.Id != 0 && t.CardTypeId == catType.Id))))
        {
            card.CardTypes.Add(new CardCardType
            {
                Card = card,
                CardType = catType,
                SourceRole = "Type"
            });
        }

        // 3. SubTypes
        var desiredSubTypes = new List<SubType>();
        if (!string.IsNullOrWhiteSpace(cardDetail.Stage))
        {
            desiredSubTypes.Add(catalog.GetOrCreateSubType(cardDetail.Stage.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(cardDetail.TrainerType))
        {
            desiredSubTypes.Add(catalog.GetOrCreateSubType(cardDetail.TrainerType.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(cardDetail.EnergyType))
        {
            desiredSubTypes.Add(catalog.GetOrCreateSubType(cardDetail.EnergyType.Trim()));
        }
        desiredSubTypes = desiredSubTypes.Distinct().ToList();

        foreach (var existing in card.CardSubTypes.ToList())
        {
            if (!desiredSubTypes.Any(st => st == existing.SubType || (st.Id != 0 && st.Id == existing.SubTypeId)))
            {
                card.CardSubTypes.Remove(existing);
            }
        }

        foreach (var subType in desiredSubTypes)
        {
            if (!card.CardSubTypes.Any(st => st.SubType == subType || (subType.Id != 0 && st.SubTypeId == subType.Id)))
            {
                card.CardSubTypes.Add(new CardSubType
                {
                    Card = card,
                    SubType = subType
                });
            }
        }
    }

    private static string BuildPrintingUid(string cardId, string treatmentName, TcgdexVariantDetailed? variantDetail)
    {
        var sourceId = cardId.Trim();
        if (variantDetail == null)
        {
            return $"{sourceId}:{BuildVariantSlug(treatmentName)}";
        }

        var variantIdentity = !string.IsNullOrWhiteSpace(variantDetail.VariantId)
            ? $"id|{NormalizeVariantPart(variantDetail.VariantId)}"
            : string.Join("|",
                NormalizeVariantPart(variantDetail.Type),
                NormalizeVariantPart(variantDetail.Subtype),
                NormalizeVariantPart(variantDetail.Size),
                NormalizeVariantPart(variantDetail.Foil),
                NormalizeVariantList(variantDetail.Stamp));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(variantIdentity)), 0, 8).ToLowerInvariant();
        return $"{sourceId}:variant:{hash}";
    }

    private static bool IsPrintingIdentityMatch(
        CardPrinting printing,
        string cardId,
        string printingUid,
        string treatmentName,
        TcgdexVariantDetailed? variantDetail)
    {
        if (string.Equals(printing.CardUid, printingUid, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                printing.CardUid,
                $"{cardId.Trim()}:{BuildVariantSlug(treatmentName)}",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (variantDetail == null || string.IsNullOrWhiteSpace(printing.DetailsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(printing.DetailsJson);
            var root = document.RootElement;
            if (!string.Equals(
                    GetJsonString(root, "tcgdexId"),
                    cardId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var storedVariantId = GetJsonString(root, "variantId");
            if (!string.IsNullOrWhiteSpace(storedVariantId) &&
                !string.IsNullOrWhiteSpace(variantDetail.VariantId) &&
                string.Equals(storedVariantId, variantDetail.VariantId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return MatchesStoredVariantPart(root, "variantType", variantDetail.Type) &&
                   MatchesStoredVariantPart(root, "variantSubtype", variantDetail.Subtype) &&
                   MatchesStoredVariantPart(root, "variantSize", variantDetail.Size) &&
                   MatchesStoredVariantPart(root, "variantFoil", variantDetail.Foil) &&
                   MatchesStoredVariantStamps(root, variantDetail.Stamp);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool MatchesStoredVariantPart(JsonElement root, string propertyName, string? value)
    {
        if (!root.TryGetProperty(propertyName, out var storedValue) || storedValue.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return string.Equals(
            NormalizeVariantPart(storedValue.GetString()),
            NormalizeVariantPart(value),
            StringComparison.Ordinal);
    }

    private static bool MatchesStoredVariantStamps(JsonElement root, IEnumerable<string>? stamps)
    {
        if (!root.TryGetProperty("variantStamps", out var storedStamps) || storedStamps.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        var storedValues = storedStamps.ValueKind == JsonValueKind.Array
            ? NormalizeVariantList(storedStamps.EnumerateArray().Select(value => value.GetString()))
            : string.Empty;
        return string.Equals(storedValues, NormalizeVariantList(stamps), StringComparison.Ordinal);
    }

    private static string? GetJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string NormalizeVariantPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(" ", value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private static string NormalizeVariantList(IEnumerable<string?>? values)
    {
        return values == null
            ? string.Empty
            : string.Join(",",
                values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeVariantPart)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string BuildVariantSlug(string treatmentName)
    {
        var sb = new StringBuilder();
        foreach (var ch in treatmentName.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
            }
        }
        return sb.Length > 0 ? sb.ToString() : "standard";
    }

    private static List<(string TreatmentName, bool IsFoil, TcgdexVariantDetailed? Detail)> DetermineVariants(TcgdexCard cardDetail)
    {
        var list = new List<(string TreatmentName, bool IsFoil, TcgdexVariantDetailed? Detail)>();

        if (cardDetail.VariantsDetailed is { Count: > 0 })
        {
            var playableVariants = cardDetail.VariantsDetailed
                .Where(v => !string.Equals(v.Size, "jumbo", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (playableVariants.Count == 0)
            {
                playableVariants = cardDetail.VariantsDetailed;
            }

            int firstEditionCount = playableVariants.Count(HasFirstEditionMarker);

            foreach (var v in playableVariants)
            {
                var (baseFinish, isBaseFoil) = MapBaseVariantType(v.Type);
                bool isFoil = isBaseFoil || !string.IsNullOrWhiteSpace(v.Foil);

                var qualifiers = new List<string>();
                bool isFirstEd = HasFirstEditionMarker(v);
                if (isFirstEd)
                {
                    qualifiers.Add("1st Edition");
                }

                if (!string.IsNullOrWhiteSpace(v.Subtype) &&
                    !string.Equals(v.Subtype, "unlimited", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(v.Subtype, "1st-edition", StringComparison.OrdinalIgnoreCase))
                {
                    var subtypeLabel = FormatVariantToken(v.Subtype);
                    if (!qualifiers.Contains(subtypeLabel, StringComparer.OrdinalIgnoreCase))
                    {
                        qualifiers.Add(subtypeLabel);
                    }
                }

                if (!string.IsNullOrWhiteSpace(v.Foil))
                {
                    var foilLabel = FormatVariantToken(v.Foil);
                    if (!qualifiers.Contains(foilLabel, StringComparer.OrdinalIgnoreCase))
                    {
                        qualifiers.Add(foilLabel);
                    }
                }

                if (v.Stamp != null)
                {
                    foreach (var stamp in v.Stamp.Where(s =>
                                 !string.IsNullOrWhiteSpace(s) &&
                                 !string.Equals(s, "1st-edition", StringComparison.OrdinalIgnoreCase)))
                    {
                        var stampLabel = FormatVariantToken(stamp);
                        if (!qualifiers.Contains(stampLabel, StringComparer.OrdinalIgnoreCase))
                        {
                            qualifiers.Add(stampLabel);
                        }
                    }
                }

                string treatmentName;
                if (qualifiers.Count == 0)
                {
                    treatmentName = baseFinish;
                }
                else if (qualifiers.Count == 1 &&
                         string.Equals(qualifiers[0], "1st Edition", StringComparison.OrdinalIgnoreCase) &&
                         firstEditionCount == 1)
                {
                    treatmentName = "1st Edition";
                }
                else
                {
                    treatmentName = $"{string.Join(" ", qualifiers)} {baseFinish}";
                }

                if (!list.Any(x => string.Equals(x.TreatmentName, treatmentName, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add((treatmentName, isFoil, v));
                }
            }

            if (list.Count > 0)
            {
                return list;
            }
        }

        var variants = cardDetail.Variants;
        if (variants == null)
        {
            list.Add(("Standard", false, null));
            return list;
        }

        if (variants.Normal) list.Add(("Standard", false, null));
        if (variants.Reverse) list.Add(("Reverse Holo", true, null));
        if (variants.Holo) list.Add(("Holo", true, null));
        if (variants.FirstEdition) list.Add(("1st Edition", variants.Holo, null));
        if (variants.WPromo) list.Add(("W Promo", variants.Holo, null));

        if (list.Count == 0)
        {
            list.Add(("Standard", false, null));
        }

        return list;
    }

    private static bool HasFirstEditionMarker(TcgdexVariantDetailed v) =>
        string.Equals(v.Subtype, "1st-edition", StringComparison.OrdinalIgnoreCase) ||
        (v.Stamp != null && v.Stamp.Any(s => string.Equals(s, "1st-edition", StringComparison.OrdinalIgnoreCase)));

    private static (string BaseFinish, bool IsFoil) MapBaseVariantType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return ("Standard", false);
        return type.Trim().ToLowerInvariant() switch
        {
            "normal" => ("Standard", false),
            "holo" => ("Holo", true),
            "reverse" => ("Reverse Holo", true),
            "metal" => ("Metal", true),
            "lenticular" => ("Lenticular", true),
            _ => (FormatVariantToken(type), true)
        };
    }

    private static string FormatVariantToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1st-edition" => "1st Edition",
            "shadowless" => "Shadowless",
            "1999-2000-copyright" => "1999-2000",
            "pokeball" => "Poké Ball",
            "masterball" => "Master Ball",
            "cosmos" => "Cosmos",
            "cracked-ice" => "Cracked Ice",
            "pre-release" => "Prerelease",
            "staff" => "Staff",
            "pokemon-center" => "Pokémon Center",
            "set-logo" => "Set Stamp",
            "w-promo" => "W Promo",
            "pokemon-together" => "Pokémon Together",
            "snowflake" => "Snowflake",
            _ => string.Join(" ",
                normalized
                    .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => char.ToUpperInvariant(w[0]) + w[1..]))
        };
    }
}
