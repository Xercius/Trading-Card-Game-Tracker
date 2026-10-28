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
    /// <param name="trait">Optional comma-separated list of associated card traits to filter by.</param>
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
        [FromQuery] string? trait = null,
        [FromQuery] string? q = null,
        [FromQuery] bool printingSearch = false,
        CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);
        var types = CsvUtils.Parse(type);
        var arenas = CsvUtils.Parse(arena);
        var aspects = CsvUtils.Parse(aspect)
            .Where(a => !string.Equals(a, "All", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var traits = CsvUtils.Parse(trait);
        var gamesNormalized = games.Select(SqliteCaseNormalizer.Normalize).ToList();
        var swuGameIds = await _db.Games
            .Where(candidate => candidate.NameNormalized == "star wars unlimited")
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
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.CardType, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)) ||
                    (cp.Number != null && EF.Functions.Like(EF.Functions.Collate(cp.Number, "NOCASE"), pattern)) ||
                    (cp.Set != null && EF.Functions.Like(EF.Property<string>(cp, "SetNorm"), normalizedPattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Name != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Name, "NOCASE"), pattern)) ||
                    (cp.CatalogSet != null && cp.CatalogSet.Code != null &&
                        EF.Functions.Like(EF.Functions.Collate(cp.CatalogSet.Code, "NOCASE"), pattern)));
            }
            else
            {
                baseQuery = baseQuery.Where(cp =>
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                    EF.Functions.Like(EF.Functions.Collate(cp.Card.CardType, "NOCASE"), pattern) ||
                    cp.Card.CardTypes.Any(cardType => EF.Functions.Like(
                        EF.Functions.Collate(cardType.CardType.Name, "NOCASE"), pattern)));
            }
        }

        // Compute game facets
        var gameQuery = ApplySetFilter(baseQuery, sets);
        gameQuery = ApplyRarityFilter(gameQuery, rarities);
        gameQuery = ApplyTypeFilter(gameQuery, types);
        gameQuery = ApplyArenaFilter(gameQuery, arenas);
        gameQuery = ApplyAspectFilter(gameQuery, aspects);
        gameQuery = ApplyTraitFilter(gameQuery, traits);

        var gameFacets = await gameQuery
            .GroupBy(cp => cp.CatalogSet == null ? cp.Card.Game : cp.CatalogSet.Game.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute set facets
        var setQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        setQuery = ApplyRarityFilter(setQuery, rarities);
        setQuery = ApplyTypeFilter(setQuery, types);
        setQuery = ApplyArenaFilter(setQuery, arenas);
        setQuery = ApplyAspectFilter(setQuery, aspects);
        setQuery = ApplyTraitFilter(setQuery, traits);

        var setFacets = await setQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.CatalogSet == null ? cp.Set : cp.CatalogSet.Code))
            .GroupBy(cp => cp.CatalogSet == null ? cp.Set : cp.CatalogSet.Code)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute rarity facets
        var rarityQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        rarityQuery = ApplySetFilter(rarityQuery, sets);
        rarityQuery = ApplyTypeFilter(rarityQuery, types);
        rarityQuery = ApplyArenaFilter(rarityQuery, arenas);
        rarityQuery = ApplyAspectFilter(rarityQuery, aspects);
        rarityQuery = ApplyTraitFilter(rarityQuery, traits);

        var rarityFacets = await rarityQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.RarityLookup == null ? cp.Rarity : cp.RarityLookup.Name))
            .GroupBy(cp => cp.RarityLookup == null ? cp.Rarity : cp.RarityLookup.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute type facets
        var typeQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        typeQuery = ApplySetFilter(typeQuery, sets);
        typeQuery = ApplyRarityFilter(typeQuery, rarities);
        typeQuery = ApplyArenaFilter(typeQuery, arenas);
        typeQuery = ApplyAspectFilter(typeQuery, aspects);
        typeQuery = ApplyTraitFilter(typeQuery, traits);

        var typePrintingCounts = await GetPrintingCountsByCardAsync(typeQuery, ct);
        var typeCardIds = typePrintingCounts.Keys.ToList();
        var typeAssociations = await _db.CardCardTypes
            .AsNoTracking()
            .Where(association => typeCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.CardType.Name })
            .ToListAsync(ct);
        var scalarSwuTypes = await _db.Cards
            .AsNoTracking()
            .Where(c => typeCardIds.Contains(c.Id) &&
                EF.Property<string>(c, "GameNorm") == "star wars unlimited")
            .Select(c => new { c.Id, c.CardType, c.CardType2 })
            .ToListAsync(ct);
        var typeFacets = BuildFacets(
            typeAssociations.Select(a => (a.CardId, a.Name))
                .Concat(scalarSwuTypes
                    .Where(c => !string.IsNullOrWhiteSpace(c.CardType))
                    .Select(c => (c.Id, c.CardType)))
                .Concat(scalarSwuTypes
                    .Where(c => !string.IsNullOrWhiteSpace(c.CardType2))
                    .Select(c => (c.Id, c.CardType2!))),
            typePrintingCounts);

        // Compute arena facets
        var arenaQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        arenaQuery = ApplySetFilter(arenaQuery, sets);
        arenaQuery = ApplyRarityFilter(arenaQuery, rarities);
        arenaQuery = ApplyTypeFilter(arenaQuery, types);
        arenaQuery = ApplyAspectFilter(arenaQuery, aspects);
        arenaQuery = ApplyTraitFilter(arenaQuery, traits);

        var arenaPrintingCounts = await GetPrintingCountsByCardAsync(arenaQuery, ct);
        var arenaCardIds = arenaPrintingCounts.Keys.ToList();
        var arenaAssociations = await _db.CardArenas
            .AsNoTracking()
            .Where(association => arenaCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Arena.Name })
            .ToListAsync(ct);
        var scalarArenas = await _db.Cards
            .AsNoTracking()
            .Where(c => arenaCardIds.Contains(c.Id) && c.Arena != null && c.Arena != "")
            .Select(c => new { CardId = c.Id, Name = c.Arena! })
            .ToListAsync(ct);
        var arenaFacets = BuildFacets(
            arenaAssociations.Select(a => (a.CardId, a.Name))
                .Concat(scalarArenas.Select(a => (a.CardId, a.Name))),
            arenaPrintingCounts);

        // Compute aspect facets
        var aspectQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        aspectQuery = ApplySetFilter(aspectQuery, sets);
        aspectQuery = ApplyRarityFilter(aspectQuery, rarities);
        aspectQuery = ApplyTypeFilter(aspectQuery, types);
        aspectQuery = ApplyArenaFilter(aspectQuery, arenas);
        aspectQuery = ApplyTraitFilter(aspectQuery, traits);

        var aspectPrintingCounts = await GetPrintingCountsByCardAsync(aspectQuery, ct);
        var aspectCardIds = aspectPrintingCounts.Keys.ToList();
        var aspectAssociations = await _db.CardAspects
            .AsNoTracking()
            .Where(association => aspectCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Aspect.Name })
            .ToListAsync(ct);
        var aspectFacets = BuildFacets(aspectAssociations.Select(a => (a.CardId, a.Name)), aspectPrintingCounts);

        // Compute trait facets
        var traitQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        traitQuery = ApplySetFilter(traitQuery, sets);
        traitQuery = ApplyRarityFilter(traitQuery, rarities);
        traitQuery = ApplyTypeFilter(traitQuery, types);
        traitQuery = ApplyArenaFilter(traitQuery, arenas);
        traitQuery = ApplyAspectFilter(traitQuery, aspects);

        var traitPrintingCounts = await GetPrintingCountsByCardAsync(traitQuery, ct);
        var traitCardIds = traitPrintingCounts.Keys.ToList();
        var traitAssociations = await _db.CardTraits
            .AsNoTracking()
            .Where(association => traitCardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.Trait.Name })
            .ToListAsync(ct);
        var traitFacets = BuildFacets(traitAssociations.Select(a => (a.CardId, a.Name)), traitPrintingCounts);

        var treatmentQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        treatmentQuery = ApplySetFilter(treatmentQuery, sets);
        treatmentQuery = ApplyRarityFilter(treatmentQuery, rarities);
        treatmentQuery = ApplyTypeFilter(treatmentQuery, types);
        treatmentQuery = ApplyArenaFilter(treatmentQuery, arenas);
        treatmentQuery = ApplyAspectFilter(treatmentQuery, aspects);
        treatmentQuery = ApplyTraitFilter(treatmentQuery, traits);
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
            .Select(cp => cp.CatalogSet == null ? cp.Card.Game : cp.CatalogSet.Game.Name)
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
        var gameIds = await _db.Games
            .Where(candidate => normalizedGames.Contains(candidate.NameNormalized))
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), normalizedGames, gameIds);

        var sets = await query
            .Select(cp => cp.CatalogSet == null ? cp.Set : cp.CatalogSet.Code)
            .Where(s => !string.IsNullOrWhiteSpace(s))
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
        var gameIds = await _db.Games
            .Where(candidate => normalizedGames.Contains(candidate.NameNormalized))
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);
        var query = ApplyGameFilter(_db.CardPrintings.AsNoTracking(), normalizedGames, gameIds);

        var rarities = await query
            .Select(cp => cp.RarityLookup == null ? cp.Rarity : cp.RarityLookup.Name)
            .Where(r => !string.IsNullOrWhiteSpace(r))
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
        IReadOnlyCollection<string> normalizedGames,
        IReadOnlyCollection<int> swuGameIds)
    {
        if (normalizedGames.Count == 0) return query;
        var legacyGames = normalizedGames.Where(value => value != "star wars unlimited").ToList();
        var includesSwu = normalizedGames.Contains("star wars unlimited");
        return query.Where(printing =>
            legacyGames.Contains(EF.Property<string>(printing.Card, "GameNorm")) ||
            (includesSwu && (
                (printing.CatalogSet != null && swuGameIds.Contains(printing.CatalogSet.GameId)) ||
                (printing.CatalogSet == null && (
                    printing.Card.CardTypes.Any(type => swuGameIds.Contains(type.CardType.GameId)) ||
                    EF.Property<string>(printing.Card, "GameNorm") == "star wars unlimited")))));
    }

    private static IQueryable<CardPrinting> ApplySetFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> sets)
    {
        if (sets.Count == 0) return query;
        var normalizedSets = sets.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.CatalogSet != null
            ? normalizedSets.Contains(EF.Functions.Collate(printing.CatalogSet.Code, "NOCASE")) ||
              (printing.CatalogSet.Name != null && normalizedSets.Contains(
                  EF.Functions.Collate(printing.CatalogSet.Name, "NOCASE")))
            : normalizedSets.Contains(EF.Property<string>(printing, "SetNorm")));
    }

    private static IQueryable<CardPrinting> ApplyRarityFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> rarities)
    {
        if (rarities.Count == 0) return query;
        var normalizedRarities = rarities.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.RarityLookup != null
            ? normalizedRarities.Contains(EF.Functions.Collate(printing.RarityLookup.Name, "NOCASE"))
            : normalizedRarities.Contains(EF.Property<string>(printing, "RarityNorm")));
    }

    private static IQueryable<CardPrinting> ApplyTypeFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> types)
    {
        if (types.Count == 0) return query;
        var normalizedTypes = types.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing =>
            normalizedTypes.Contains(EF.Functions.Collate(printing.Card.CardType, "NOCASE")) ||
            (printing.Card.CardType2 != null &&
                normalizedTypes.Contains(EF.Functions.Collate(printing.Card.CardType2, "NOCASE"))) ||
            printing.Card.CardTypes.Any(cardType =>
                normalizedTypes.Contains(EF.Functions.Collate(cardType.CardType.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyArenaFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> arenas)
    {
        if (arenas.Count == 0) return query;
        var normalizedArenas = arenas.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing =>
            (printing.Card.Arena != null &&
                normalizedArenas.Contains(EF.Functions.Collate(printing.Card.Arena, "NOCASE"))) ||
            printing.Card.CardArenas.Any(cardArena =>
                normalizedArenas.Contains(EF.Functions.Collate(cardArena.Arena.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyAspectFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> aspects)
    {
        if (aspects.Count == 0) return query;
        var normalizedAspects = aspects
            .Select(SqliteCaseNormalizer.Normalize)
            .Where(a => a != "all")
            .ToList();
        if (normalizedAspects.Count == 0) return query;
        return query.Where(printing => printing.Card.CardAspects.Any(cardAspect =>
            normalizedAspects.Contains(EF.Functions.Collate(cardAspect.Aspect.Name, "NOCASE"))));
    }

    private static IQueryable<CardPrinting> ApplyTraitFilter(
        IQueryable<CardPrinting> query,
        IReadOnlyCollection<string> traits)
    {
        if (traits.Count == 0) return query;
        var normalizedTraits = traits.Select(SqliteCaseNormalizer.Normalize).ToList();
        return query.Where(printing => printing.Card.CardTraits.Any(cardTrait =>
            normalizedTraits.Contains(EF.Functions.Collate(cardTrait.Trait.Name, "NOCASE"))));
    }
}
