using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Models;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

/// <summary>
/// Provides API endpoints for retrieving filter facets (games, sets, rarities, types, arenas, aspects, traits) used in card search and browsing.
/// This controller supports the client-side filtering UI by returning distinct values from the card database
/// that can be used to populate dropdown filters and refine search results.
/// </summary>
[ApiController]
[Route("api/cards/facets")]
public sealed class CardFacetsController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFacetsController"/> class.
    /// </summary>
    /// <param name="db">The database context for accessing card and printing data.</param>
    public CardFacetsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves facets (games, sets, rarities, types, arenas, aspects, traits) with counts based on active filters.
    /// Only returns facet values that produce results with the current filter combination.
    /// </summary>
    /// <param name="game">Optional comma-separated list of game names to filter by.</param>
    /// <param name="set">Optional comma-separated list of set names to filter by.</param>
    /// <param name="rarity">Optional comma-separated list of rarity values to filter by.</param>
    /// <param name="type">Optional comma-separated list of associated card types to filter by.</param>
    /// <param name="arena">Optional comma-separated list of associated card arenas to filter by.</param>
    /// <param name="aspect">Optional comma-separated list of associated card aspects to filter by.</param>
    /// <param name="aspectMethod">Filtering method for aspects ('or', 'and', 'exclude').</param>
    /// <param name="trait">Optional comma-separated list of associated card traits to filter by.</param>
    /// <param name="traitMethod">Filtering method for traits ('or', 'and', 'exclude').</param>
    /// <param name="keyword">Optional comma-separated list of associated card keywords to filter by.</param>
    /// <param name="keywordMethod">Filtering method for keywords ('or', 'and', 'exclude').</param>
    /// <param name="q">Optional search term to filter cards or printings by.</param>
    /// <param name="printingSearch">Whether to also match printing number and set alongside card name and type.</param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardsFacetResponse"/> containing
    /// lists of games, sets, rarities, types, arenas, aspects, and traits with their respective counts.
    /// Each facet list reflects the current filter state and only includes values that yield results.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<CardsFacetResponse>> GetFacets(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? type,
        [FromQuery] string? arena = null,
        [FromQuery] string? aspect = null,
        [FromQuery] string? aspectMethod = null,
        [FromQuery] string? trait = null,
        [FromQuery] string? traitMethod = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? keywordMethod = null,
        [FromQuery] string? q = null,
        [FromQuery] bool printingSearch = false,
        CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);
        var types = CsvUtils.Parse(type);
        var arenas = CsvUtils.Parse(arena);
        var aspects = CsvUtils.ParsePreservingDuplicates(aspect)
            .Where(a => !string.Equals(a, "All", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var traits = CsvUtils.Parse(trait);
        var keywords = CsvUtils.Parse(keyword);
        var gamesNormalized = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => gamesNormalized.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);

        // Build base query with filters
        var baseQuery = _db.CardPrintings.AsNoTracking().AsQueryable();

        // Apply search term filter (case-insensitive)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var pattern = $"%{term}%";
            if (printingSearch)
            {
                var normalizedTerm = SqliteCaseNormalizer.Normalize(term);
                var normalizedPattern = $"%{normalizedTerm}%";
                baseQuery = baseQuery.Where(cp =>
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)) ||
                    (cp.Number != null && EF.Functions.Like(EF.Functions.Collate(cp.Number, "NOCASE"), pattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Name != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Name, "NOCASE"), pattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Code != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Code, "NOCASE"), pattern)));
            }
            else
            {
                baseQuery = baseQuery.Where(cp =>
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)));
            }
        }

        // Compute game facets
        var gameQuery = ApplySetFilter(baseQuery, sets);
        gameQuery = ApplyRarityFilter(gameQuery, rarities);
        gameQuery = ApplyTypeFilter(gameQuery, types);
        gameQuery = ApplyArenaFilter(gameQuery, arenas);
        gameQuery = ApplyAspectFilter(gameQuery, aspects, aspectMethod);
        gameQuery = ApplyTraitFilter(gameQuery, traits, traitMethod);
        gameQuery = ApplyKeywordFilter(gameQuery, keywords, keywordMethod);

        var gameFacets = await gameQuery
            .GroupBy(cp => cp.Card.Game!.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute set facets
        var setQuery = ApplyGameFilter(baseQuery, gameIds);
        setQuery = ApplyRarityFilter(setQuery, rarities);
        setQuery = ApplyTypeFilter(setQuery, types);
        setQuery = ApplyArenaFilter(setQuery, arenas);
        setQuery = ApplyAspectFilter(setQuery, aspects, aspectMethod);
        setQuery = ApplyTraitFilter(setQuery, traits, traitMethod);
        setQuery = ApplyKeywordFilter(setQuery, keywords, keywordMethod);

        var setFacets = await setQuery
            .Where(cp => cp.CatalogSet != null && !string.IsNullOrWhiteSpace(cp.CatalogSet.Code))
            .GroupBy(cp => cp.CatalogSet!.Code)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute rarity facets
        var rarityQuery = ApplyGameFilter(baseQuery, gameIds);
        rarityQuery = ApplySetFilter(rarityQuery, sets);
        rarityQuery = ApplyTypeFilter(rarityQuery, types);
        rarityQuery = ApplyArenaFilter(rarityQuery, arenas);
        rarityQuery = ApplyAspectFilter(rarityQuery, aspects, aspectMethod);
        rarityQuery = ApplyTraitFilter(rarityQuery, traits, traitMethod);
        rarityQuery = ApplyKeywordFilter(rarityQuery, keywords, keywordMethod);

        var rarityFacets = await rarityQuery
            .Where(cp => cp.RarityLookup != null && !string.IsNullOrWhiteSpace(cp.RarityLookup.Name))
            .GroupBy(cp => cp.RarityLookup!.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute type facets
        var typeQuery = ApplyGameFilter(baseQuery, gameIds);
        typeQuery = ApplySetFilter(typeQuery, sets);
        typeQuery = ApplyRarityFilter(typeQuery, rarities);
        typeQuery = ApplyArenaFilter(typeQuery, arenas);
        typeQuery = ApplyAspectFilter(typeQuery, aspects, aspectMethod);
        typeQuery = ApplyTraitFilter(typeQuery, traits, traitMethod);
        typeQuery = ApplyKeywordFilter(typeQuery, keywords, keywordMethod);

        var typePrintingCounts = await GetPrintingCountsByCardAsync(typeQuery, ct);
        var typeCardIds = typePrintingCounts.Keys.ToList();
        var typeAssociations = await _db.CardCardTypes
            .AsNoTracking()
            .Where(association => typeCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.CardType.Name })
            .ToListAsync(ct);
        var typeFacets = BuildFacets(
            typeAssociations.Select(a => (a.CardId, a.Name)),
            typePrintingCounts);

        // Compute arena facets
        var arenaQuery = ApplyGameFilter(baseQuery, gameIds);
        arenaQuery = ApplySetFilter(arenaQuery, sets);
        arenaQuery = ApplyRarityFilter(arenaQuery, rarities);
        arenaQuery = ApplyTypeFilter(arenaQuery, types);
        arenaQuery = ApplyAspectFilter(arenaQuery, aspects, aspectMethod);
        arenaQuery = ApplyTraitFilter(arenaQuery, traits, traitMethod);
        arenaQuery = ApplyKeywordFilter(arenaQuery, keywords, keywordMethod);

        var arenaPrintingCounts = await GetPrintingCountsByCardAsync(arenaQuery, ct);
        var arenaCardIds = arenaPrintingCounts.Keys.ToList();
        var arenaAssociations = await _db.CardArenas
            .AsNoTracking()
            .Where(association => arenaCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Arena.Name })
            .ToListAsync(ct);
        var arenaFacets = BuildFacets(
            arenaAssociations.Select(a => (a.CardId, a.Name)),
            arenaPrintingCounts);

        // Compute aspect facets
        var aspectQuery = ApplyGameFilter(baseQuery, gameIds);
        aspectQuery = ApplySetFilter(aspectQuery, sets);
        aspectQuery = ApplyRarityFilter(aspectQuery, rarities);
        aspectQuery = ApplyTypeFilter(aspectQuery, types);
        aspectQuery = ApplyArenaFilter(aspectQuery, arenas);
        aspectQuery = ApplyTraitFilter(aspectQuery, traits, traitMethod);
        aspectQuery = ApplyKeywordFilter(aspectQuery, keywords, keywordMethod);

        var aspectPrintingCounts = await GetPrintingCountsByCardAsync(aspectQuery, ct);
        var aspectCardIds = aspectPrintingCounts.Keys.ToList();
        var aspectAssociations = await _db.CardAspects
            .AsNoTracking()
            .Where(association => aspectCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Aspect.Name })
            .ToListAsync(ct);
        var aspectFacets = BuildFacets(aspectAssociations.Select(a => (a.CardId, a.Name)), aspectPrintingCounts);

        // Compute trait facets
        var traitQuery = ApplyGameFilter(baseQuery, gameIds);
        traitQuery = ApplySetFilter(traitQuery, sets);
        traitQuery = ApplyRarityFilter(traitQuery, rarities);
        traitQuery = ApplyTypeFilter(traitQuery, types);
        traitQuery = ApplyArenaFilter(traitQuery, arenas);
        traitQuery = ApplyAspectFilter(traitQuery, aspects, aspectMethod);
        traitQuery = ApplyKeywordFilter(traitQuery, keywords, keywordMethod);

        var traitPrintingCounts = await GetPrintingCountsByCardAsync(traitQuery, ct);
        var traitCardIds = traitPrintingCounts.Keys.ToList();
        var traitAssociations = await _db.CardTraits
            .AsNoTracking()
            .Where(association => traitCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Trait.Name })
            .ToListAsync(ct);
        var traitFacets = BuildFacets(traitAssociations.Select(a => (a.CardId, a.Name)), traitPrintingCounts);

        // Compute keyword facets
        var keywordQuery = ApplyGameFilter(baseQuery, gameIds);
        keywordQuery = ApplySetFilter(keywordQuery, sets);
        keywordQuery = ApplyRarityFilter(keywordQuery, rarities);
        keywordQuery = ApplyTypeFilter(keywordQuery, types);
        keywordQuery = ApplyArenaFilter(keywordQuery, arenas);
        keywordQuery = ApplyAspectFilter(keywordQuery, aspects, aspectMethod);
        keywordQuery = ApplyTraitFilter(keywordQuery, traits, traitMethod);

        var keywordPrintingCounts = await GetPrintingCountsByCardAsync(keywordQuery, ct);
        var keywordCardIds = keywordPrintingCounts.Keys.ToList();
        var keywordAssociations = await _db.CardKeywords
            .AsNoTracking()
            .Where(association => keywordCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Keyword.Name })
            .ToListAsync(ct);
        var keywordFacets = BuildFacets(keywordAssociations.Select(a => (a.CardId, a.Name)), keywordPrintingCounts);

        var treatmentQuery = ApplyGameFilter(baseQuery, gameIds);
        treatmentQuery = ApplySetFilter(treatmentQuery, sets);
        treatmentQuery = ApplyRarityFilter(treatmentQuery, rarities);
        treatmentQuery = ApplyTypeFilter(treatmentQuery, types);
        treatmentQuery = ApplyArenaFilter(treatmentQuery, arenas);
        treatmentQuery = ApplyAspectFilter(treatmentQuery, aspects, aspectMethod);
        treatmentQuery = ApplyTraitFilter(treatmentQuery, traits, traitMethod);
        treatmentQuery = ApplyKeywordFilter(treatmentQuery, keywords, keywordMethod);
        var treatmentFacets = await treatmentQuery
            .Where(printing => printing.Treatment != null)
            .GroupBy(printing => printing.Treatment!.Name)
            .Select(group => new FacetOption { Value = group.Key, Count = group.Count() })
            .OrderBy(facet => facet.Value)
            .ToListAsync(ct);

        var response = new CardsFacetResponse
        {
            Games = gameFacets,
            Sets = setFacets,
            Rarities = rarityFacets,
            Types = typeFacets,
            Arenas = arenaFacets,
            Aspects = aspectFacets,
            Traits = traitFacets,
            Keywords = keywordFacets,
            Treatments = treatmentFacets,
        };

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of all games available in the card database.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a list of game names in alphabetical order.
    /// The list includes all unique games from card printings in the database.
    /// </returns>
    [HttpGet("games")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetGames(CancellationToken ct = default)
    {
        var games = await _db.CardPrintings
            .AsNoTracking()
            .Select(cp => cp.Card.Game!.Name)
            .Distinct()
            .OrderBy(game => game)
            .ToListAsync(ct);

        return Ok(games);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of card sets, optionally filtered by game(s).
    /// </summary>
    /// <param name="game">
    /// Optional comma-separated list of game names to filter by. If provided, only sets from those games are returned.
    /// If empty or null, returns sets from all games.
    /// </param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardFacetSetsResponse"/> containing:
    /// - A list of set names in alphabetical order
    /// - The game name (if exactly one game was specified in the filter)
    /// Excludes any sets with null or whitespace names.
    /// </returns>
    [HttpGet("sets")]
    public async Task<ActionResult<CardFacetSetsResponse>> GetSets([FromQuery] string? game, CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => normalizedGames.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), gameIds);

        var sets = await query
            .Select(cp => cp.CatalogSet == null ? null : cp.CatalogSet.Code)
            .Where(s => s != null && !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .OrderBy(set => set)
            .ToListAsync(ct);

        var response = new CardFacetSetsResponse
        {
            Game = games.Count == 1 ? games[0] : null,
            Sets = sets,
        };

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a distinct, sorted list of card rarities, optionally filtered by game(s).
    /// </summary>
    /// <param name="game">
    /// Optional comma-separated list of game names to filter by. If provided, only rarities from those games are returned.
    /// If empty or null, returns rarities from all games.
    /// </param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardFacetRaritiesResponse"/> containing:
    /// - A list of rarity values in alphabetical order
    /// - The game name (if exactly one game was specified in the filter)
    /// Excludes any rarities with null or whitespace values.
    /// </returns>
    [HttpGet("rarities")]
    public async Task<ActionResult<CardFacetRaritiesResponse>> GetRarities([FromQuery] string? game, CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var normalizedGames = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var gameLookupQuery = _db.Games.AsQueryable();
        if (games.Count > 0)
            gameLookupQuery = gameLookupQuery.Where(candidate => normalizedGames.Contains(candidate.NameNormalized));
        var gameIds = await gameLookupQuery
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), gameIds);

        var rarities = await query
            .Select(cp => cp.RarityLookup == null ? null : cp.RarityLookup.Name)
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
            .Distinct()
            .OrderBy(rarity => rarity)
            .ToListAsync(ct);

        var response = new CardFacetRaritiesResponse
        {
            Game = games.Count == 1 ? games[0] : null,
            Rarities = rarities,
        };

        return Ok(response);
    }

    private static async Task<Dictionary<int, int>> GetPrintingCountsByCardAsync(
        IQueryable<CardPrinting> query,
        CancellationToken ct)
    {
        var printingCounts = await query
            .GroupBy(printing => printing.CardId)
            .Select(group => new { CardId = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        return printingCounts.ToDictionary(item => item.CardId, item => item.Count);
    }

    private static List<FacetOption> BuildFacets(
        IEnumerable<(int CardId, string Name)> associations,
        IReadOnlyDictionary<int, int> printingCountsByCard)
        => associations
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && printingCountsByCard.ContainsKey(item.CardId))
            .GroupBy(item => (item.CardId, Name: item.Name.Trim()))
            .GroupBy(group => group.Key.Name)
            .Select(group => new FacetOption
            {
                Value = group.Key,
                Count = group.Sum(item => printingCountsByCard[item.Key.CardId])
            })
            .OrderBy(facet => facet.Value)
            .ToList();

    private static IQueryable<CardPrinting> ApplyGameFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<int> gameIds)
    {
        return gameIds.Count == 0
            ? query.Where(_ => false)
            : query.Where(printing => printing.Card.GameId.HasValue
                && gameIds.Contains(printing.Card.GameId.Value));
    }

    private static IQueryable<CardPrinting> ApplySetFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> sets)
    {
        if (sets.Count == 0) return query;
        var normalizedSets = sets.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.CatalogSet != null
            && (normalizedSets.Contains(EF.Functions.Collate(printing.CatalogSet.Code, "NOCASE")) ||
                printing.CatalogSet.Name != null && normalizedSets.Contains(
                    EF.Functions.Collate(printing.CatalogSet.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyRarityFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> rarities)
    {
        if (rarities.Count == 0) return query;
        var normalizedRarities = rarities.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.RarityLookup != null
            && normalizedRarities.Contains(EF.Functions.Collate(printing.RarityLookup.Name, "NOCASE")));
    }

    private static IQueryable<CardPrinting> ApplyTypeFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> types)
    {
        if (types.Count == 0) return query;
        var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.Card.CardTypes.Any(cardType =>
                normalizedTypes.Contains(EF.Functions.Collate(cardType.CardType.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyArenaFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> arenas)
    {
        if (arenas.Count == 0) return query;
        var normalizedArenas = arenas.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.Card.CardArenas.Any(cardArena =>
                normalizedArenas.Contains(EF.Functions.Collate(cardArena.Arena.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyAspectFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> aspects,
        string? aspectMethod = null)
        => query.ApplyAspectFilter(aspects, aspectMethod);

    private static IQueryable<CardPrinting> ApplyTraitFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> traits,
        string? traitMethod = null)
        => query.ApplyTraitFilter(traits, traitMethod);

    private static IQueryable<CardPrinting> ApplyKeywordFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> keywords,
        string? keywordMethod = null)
        => query.ApplyKeywordFilter(keywords, keywordMethod);
}
