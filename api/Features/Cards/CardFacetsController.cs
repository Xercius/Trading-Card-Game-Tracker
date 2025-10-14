using api.Data;
using api.Features.Cards.Dtos;
using api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

/// <summary>
/// Provides API endpoints for retrieving filter facets (games, sets, rarities) used in card search and browsing.
/// This controller supports the client-side filtering UI by returning distinct values from the card database
/// that can be used to populate dropdown filters and refine search results.
/// </summary>
[ApiController]
[Authorize]
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
    /// <param name="q">Optional search term to filter cards by name or type.</param>
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
        [FromQuery] string? q,
        CancellationToken ct = default)
    {
        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);

        // Build base query with filters
        var baseQuery = _db.CardPrintings.AsNoTracking().AsQueryable();

        // Apply search term filter (case-insensitive)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var pattern = $"%{term}%";
            baseQuery = baseQuery.Where(cp =>
                EF.Functions.Like(EF.Functions.Collate(cp.Card.Name, "NOCASE"), pattern) ||
                EF.Functions.Like(EF.Functions.Collate(cp.Card.CardType, "NOCASE"), pattern));
        }

        // Compute game facets (with set, rarity filters applied)
        var gameQuery = baseQuery;
        if (sets.Count > 0)
        {
            gameQuery = gameQuery.Where(cp =>
                sets.Contains(EF.Functions.Collate(cp.Set, "NOCASE")));
        }
        if (rarities.Count > 0)
        {
            gameQuery = gameQuery.Where(cp =>
                rarities.Contains(EF.Functions.Collate(cp.Rarity, "NOCASE")));
        }

        var gameFacets = await gameQuery
            .GroupBy(cp => cp.Card.Game)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute set facets (with game, rarity filters applied)
        var setQuery = baseQuery;
        if (games.Count > 0)
        {
            setQuery = setQuery.Where(cp =>
                games.Contains(EF.Functions.Collate(cp.Card.Game, "NOCASE")));
        }
        if (rarities.Count > 0)
        {
            setQuery = setQuery.Where(cp =>
                rarities.Contains(EF.Functions.Collate(cp.Rarity, "NOCASE")));
        }

        var setFacets = await setQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.Set))
            .GroupBy(cp => cp.Set)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        // Compute rarity facets (with game, set filters applied)
        var rarityQuery = baseQuery;
        if (games.Count > 0)
        {
            rarityQuery = rarityQuery.Where(cp =>
                games.Contains(EF.Functions.Collate(cp.Card.Game, "NOCASE")));
        }
        if (sets.Count > 0)
        {
            rarityQuery = rarityQuery.Where(cp =>
                sets.Contains(EF.Functions.Collate(cp.Set, "NOCASE")));
        }

        var rarityFacets = await rarityQuery
            .Where(cp => !string.IsNullOrWhiteSpace(cp.Rarity))
            .GroupBy(cp => cp.Rarity)
            .Select(g => new FacetOption { Value = g.Key, Count = g.Count() })
            .OrderBy(f => f.Value)
            .ToListAsync(ct);

        var response = new CardsFacetResponse
        {
            Games = gameFacets,
            Sets = setFacets,
            Rarities = rarityFacets,
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
            .Select(cp => cp.Card.Game)
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

        var query = _db.CardPrintings.AsNoTracking().AsQueryable();
        if (games.Count > 0)
        {
            query = query.Where(cp => games.Contains(cp.Card.Game));
        }

        var sets = await query
            .Select(cp => cp.Set)
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

        var query = _db.CardPrintings.AsNoTracking().AsQueryable();
        if (games.Count > 0)
        {
            query = query.Where(cp => games.Contains(cp.Card.Game));
        }

        var rarities = await query
            .Select(cp => cp.Rarity)
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

}
