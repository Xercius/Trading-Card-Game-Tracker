using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using api.Data;
using api.Features.Cards.Dtos;
using api.Authentication;
using api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

[ApiController]
[Authorize]
[Route("api/cards/facets")]
public sealed class CardFacetsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CardFacetsController(AppDbContext db)
    {
        _db = db;
    }

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
