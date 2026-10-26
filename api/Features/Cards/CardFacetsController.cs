using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Models;
using api.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

/// <summary>
/// Provides API endpoints for retrieving filter facets (games, sets, rarities) used in card search and browsing.
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
    /// Retrieves facets (games, sets, rarities) with counts based on active filters.
    /// Only returns facet values that produce results with the current filter combination.
    /// </summary>
    /// <param name="game">Optional comma-separated list of game names to filter by.</param>
    /// <param name="set">Optional comma-separated list of set names to filter by.</param>
    /// <param name="rarity">Optional comma-separated list of rarity values to filter by.</param>
    /// <param name="type">Optional comma-separated list of associated card types to filter by.</param>
    /// <param name="q">Optional search term to filter cards or printings by.</param>
    /// <param name="printingSearch">Whether to also match printing number and set alongside card name and type.</param>
    /// <param name="ct">Cancellation token to stop the operation if needed.</param>
    /// <returns>
    /// An HTTP 200 OK response with a <see cref="CardsFacetResponse"/> containing
    /// lists of games, sets, and rarities with their respective counts.
    /// Each facet list reflects the current filter state and only includes values that yield results.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<CardsFacetResponse>> GetFacets(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] bool printingSearch = false,
        CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);
        var types = CsvUtils.Parse(type);
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

        // Compute game facets (with set, rarity filters applied)
        var gameQuery = baseQuery;
        gameQuery = ApplySetFilter(gameQuery, sets);
        gameQuery = ApplyRarityFilter(gameQuery, rarities);
        gameQuery = ApplyTypeFilter(gameQuery, types);

        var gameFacets = await gameQuery
            .GroupBy(cp => cp.CatalogSet == null ? cp.Card.Game : cp.CatalogSet.Game.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute set facets (with game, rarity filters applied)
        var setQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        setQuery = ApplyRarityFilter(setQuery, rarities);
        setQuery = ApplyTypeFilter(setQuery, types);

        var setFacets = await setQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.CatalogSet == null ? cp.Set : cp.CatalogSet.Code))
            .GroupBy(cp => cp.CatalogSet == null ? cp.Set : cp.CatalogSet.Code)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute rarity facets (with game, set filters applied)
        var rarityQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        rarityQuery = ApplySetFilter(rarityQuery, sets);
        rarityQuery = ApplyTypeFilter(rarityQuery, types);

        var rarityFacets = await rarityQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.RarityLookup == null ? cp.Rarity : cp.RarityLookup.Name))
            .GroupBy(cp => cp.RarityLookup == null ? cp.Rarity : cp.RarityLookup.Name)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        var typeQuery = ApplyGameFilter(baseQuery, gamesNormalized, swuGameIds);
        typeQuery = ApplySetFilter(typeQuery, sets);
        typeQuery = ApplyRarityFilter(typeQuery, rarities);
        var printingCounts = await typeQuery
            .GroupBy(printing => printing.CardId)
            .Select(group => new { CardId = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        var cardIds = printingCounts.Select(item => item.CardId).ToList();
        var printingCountsByCard = printingCounts.ToDictionary(item => item.CardId, item => item.Count);
        var typeAssociations = await _db.CardCardTypes
            .AsNoTracking()
            .Where(association => swuGameIds.Contains(association.CardType.GameId) &&
                cardIds.Contains(association.CardId))
            .Select(association => new { association.CardId, association.CardType.Name })
            .ToListAsync(ct);
        var typeFacets = typeAssociations
            .GroupBy(association => new { association.CardId, association.Name })
            .GroupBy(group => group.Key.Name)
            .Select(group => new FacetOption
            {
                Value = group.Key,
                Count = group.Sum(item => printingCountsByCard[item.Key.CardId])
            })
            .OrderBy(facet => facet.Value)
            .ToList();

        var response = new CardsFacetResponse
        {
            Games = gameFacets,
            Sets = setFacets,
            Rarities = rarityFacets,
            Types = typeFacets,
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
        return query.Where(printing => printing.Card.CardTypes.Any(cardType =>
            normalizedTypes.Contains(EF.Functions.Collate(cardType.CardType.Name, "NOCASE"))));
    }
}
