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

                // Fetch detail if set does not exist or lacks Era/ReleaseDate
                TcgdexSetDetail? detail = null;
                if (existing == null || string.IsNullOrWhiteSpace(existing.Era) || existing.ReleasedAt == null)
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
            var syncHistories = await _db.ImportSyncHistories
                .Where(h => h.ImporterKey == "pokemon-cards")
                .ToDictionaryAsync(h => h.SetCode.Trim(), StringComparer.OrdinalIgnoreCase, ct);

            var dbSets = await _db.Sets
                .Where(s => s.GameId == game.Id)
                .ToDictionaryAsync(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

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

                // Load existing cards & printings for this set
                var existingCardsInSet = await _db.Cards
                    .Include(c => c.CardAspects)
                    .Include(c => c.CardTypes)
                    .Include(c => c.CardSubTypes)
                    .Include(c => c.Printings)
                    .Where(c => c.GameId == game.Id && c.Printings.Any(p => p.SetId == dbSet.Id))
                    .ToDictionaryAsync(c => c.CardUid!.Trim(), StringComparer.OrdinalIgnoreCase, ct);

                int setCardsUpserted = 0;
                int setPrintingsUpserted = 0;

                foreach (var cardDetail in cardsFetched)
                {
                    if (cardDetail == null) continue;

                    DateTimeOffset cardUpdated = DateTimeOffset.MinValue;
                    if (!string.IsNullOrWhiteSpace(cardDetail.Updated) && DateTimeOffset.TryParse(cardDetail.Updated, out var u))
                    {
                        cardUpdated = u;
                    }

                    // Check if unchanged
                    existingCardsInSet.TryGetValue(cardDetail.Id.Trim(), out var existingCard);
                    if (!forceFullSync && existingCard != null && cardUpdated != DateTimeOffset.MinValue && cardUpdated <= lowerBoundTime)
                    {
                        continue; // Skip unchanged card
                    }

                    // 1. Upsert Card
                    if (existingCard == null)
                    {
                        existingCard = new Card
                        {
                            GameId = game.Id,
                            CardUid = cardDetail.Id.Trim(),
                            Name = cardDetail.Name.Trim(),
                            Hp = cardDetail.Hp,
                            Rules = catalog.BuildRulesText(cardDetail),
                            Text = cardDetail.Description,
                            ExtraCardDataJson = catalog.BuildExtraCardDataJson(cardDetail),
                            LastSyncedAt = _timeProvider.GetUtcNow()
                        };
                        _db.Cards.Add(existingCard);
                        existingCardsInSet[cardDetail.Id.Trim()] = existingCard;
                        summary.CardsCreated++;
                    }
                    else
                    {
                        existingCard.Name = cardDetail.Name.Trim();
                        existingCard.Hp = cardDetail.Hp;
                        existingCard.Rules = catalog.BuildRulesText(cardDetail);
                        existingCard.Text = cardDetail.Description;
                        existingCard.ExtraCardDataJson = catalog.BuildExtraCardDataJson(cardDetail);
                        existingCard.LastSyncedAt = _timeProvider.GetUtcNow();
                        summary.CardsUpdated++;
                    }
                    setCardsUpserted++;

                    // 2. Link Aspects (Energy Types)
                    existingCard.CardAspects.Clear();
                    if (cardDetail.Types != null)
                    {
                        foreach (var energyType in cardDetail.Types.Where(t => !string.IsNullOrWhiteSpace(t)))
                        {
                            var aspect = catalog.GetOrCreateAspect(energyType);
                            existingCard.CardAspects.Add(new CardAspect
                            {
                                Card = existingCard,
                                Aspect = aspect,
                                Count = 1
                            });
                        }
                    }

                    // 3. Link CardTypes & SubTypes
                    existingCard.CardTypes.Clear();
                    var category = !string.IsNullOrWhiteSpace(cardDetail.Category) ? cardDetail.Category.Trim() : "Pokemon";
                    var catType = catalog.GetOrCreateCardType(category);
                    existingCard.CardTypes.Add(new CardCardType
                    {
                        Card = existingCard,
                        CardType = catType,
                        SourceRole = "Type"
                    });

                    existingCard.CardSubTypes.Clear();
                    if (!string.IsNullOrWhiteSpace(cardDetail.Stage))
                    {
                        var stageSubType = catalog.GetOrCreateSubType(cardDetail.Stage.Trim());
                        existingCard.CardSubTypes.Add(new CardSubType { Card = existingCard, SubType = stageSubType });
                    }
                    if (!string.IsNullOrWhiteSpace(cardDetail.TrainerType))
                    {
                        var trainerSubType = catalog.GetOrCreateSubType(cardDetail.TrainerType.Trim());
                        existingCard.CardSubTypes.Add(new CardSubType { Card = existingCard, SubType = trainerSubType });
                    }
                    if (!string.IsNullOrWhiteSpace(cardDetail.EnergyType))
                    {
                        var energySubType = catalog.GetOrCreateSubType(cardDetail.EnergyType.Trim());
                        existingCard.CardSubTypes.Add(new CardSubType { Card = existingCard, SubType = energySubType });
                    }

                    // 4. Upsert Printings for each physical variant
                    var variants = DetermineVariants(cardDetail.Variants);
                    var rarity = catalog.GetOrCreateRarity(cardDetail.Rarity);
                    var imageUrl = !string.IsNullOrWhiteSpace(cardDetail.Image) ? $"{cardDetail.Image}/high.webp" : null;

                    foreach (var (treatmentName, isFoil) in variants)
                    {
                        var treatment = catalog.GetOrCreateTreatment(treatmentName);
                        var variantSlug = treatmentName.ToLowerInvariant().Replace(" ", "");
                        var printingUid = $"{cardDetail.Id.Trim()}:{variantSlug}";

                        var printing = existingCard.Printings.FirstOrDefault(p =>
                            p.SetId == dbSet.Id &&
                            (p.CardUid == printingUid || p.TreatmentId == treatment.Id));

                        if (printing == null)
                        {
                            printing = new CardPrinting
                            {
                                SetId = dbSet.Id,
                                Card = existingCard,
                                Number = cardDetail.LocalId.Trim(),
                                CardUid = printingUid,
                                ImageUrl = imageUrl,
                                Artist = cardDetail.Illustrator?.Trim(),
                                Treatment = treatment,
                                RarityLookup = rarity,
                                IsFoil = isFoil,
                                ApiUpdatedAt = cardUpdated != DateTimeOffset.MinValue ? cardUpdated : null
                            };
                            _db.CardPrintings.Add(printing);
                            summary.PrintingsCreated++;
                        }
                        else
                        {
                            printing.CardUid = printingUid;
                            printing.Number = cardDetail.LocalId.Trim();
                            printing.ImageUrl = imageUrl;
                            printing.Artist = cardDetail.Illustrator?.Trim();
                            printing.Treatment = treatment;
                            printing.RarityLookup = rarity;
                            printing.IsFoil = isFoil;
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

    private static List<(string TreatmentName, bool IsFoil)> DetermineVariants(TcgdexVariants? variants)
    {
        var list = new List<(string TreatmentName, bool IsFoil)>();
        if (variants == null)
        {
            list.Add(("Standard", false));
            return list;
        }

        if (variants.Normal) list.Add(("Standard", false));
        if (variants.Reverse) list.Add(("Reverse Holo", true));
        if (variants.Holo) list.Add(("Holo", true));
        if (variants.FirstEdition) list.Add(("1st Edition", variants.Holo));

        if (list.Count == 0)
        {
            list.Add(("Standard", false));
        }

        return list;
    }
}
