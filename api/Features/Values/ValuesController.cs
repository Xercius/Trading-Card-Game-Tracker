using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using api.Data;
using api.Features.Values.Dtos;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Values;

[ApiController]
[Route("api/value")]
public class ValuesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public ValuesController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh([FromQuery] string game, [FromBody] List<RefreshItemRequest> items)
    {
        if (string.IsNullOrWhiteSpace(game)) return BadRequest("game required");
        if (items == null || items.Count == 0) return BadRequest("no items");

        var cpIds = items.Select(i => i.CardPrintingId).ToHashSet();
        var valid = await _db.CardPrintings.Include(cp => cp.Card)
            .Where(cp => cpIds.Contains(cp.Id) && cp.Card.Game == game)
            .Select(cp => cp.Id)
            .ToListAsync();

        var validSet = valid.ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var i in items.Where(x => validSet.Contains(x.CardPrintingId)))
        {
            _db.ValueHistories.Add(new ValueHistory
            {
                ScopeType = ValueScopeType.CardPrinting,
                ScopeId = i.CardPrintingId,
                PriceCents = i.PriceCents,
                AsOfUtc = now,
                Source = string.IsNullOrWhiteSpace(i.Source) ? "manual" : i.Source!
            });
        }

        await _db.SaveChangesAsync();
        var inserted = items.Count(x => validSet.Contains(x.CardPrintingId));
        var ignored = items.Count - inserted;
        return Ok(new { inserted, ignored });
    }

    [HttpGet("cardprinting/{id:int}")]
    public async Task<ActionResult<SeriesResponse>> GetCardPrintingSeries(int id)
    {
        var exists = await _db.CardPrintings.AnyAsync(x => x.Id == id);
        if (!exists) return NotFound();

        var histories = await _db.ValueHistories
            .Where(v => v.ScopeType == ValueScopeType.CardPrinting && v.ScopeId == id)
            .OrderBy(v => v.AsOfUtc)
            .ToListAsync();

        var points = _mapper.Map<List<SeriesPointResponse>>(histories);
        return Ok(new SeriesResponse(id, points));
    }

    [HttpGet("collection/summary"), RequireUserHeader]
    public async Task<ActionResult<CollectionSummaryResponse>> GetCollectionSummary()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null) return BadRequest("X-User-Id header required");

        var latest = await LatestPricesAsync(ValueScopeType.CardPrinting);

        var data = await _db.UserCards
            .Include(uc => uc.CardPrinting).ThenInclude(cp => cp.Card)
            .Where(uc => uc.UserId == user.Id && uc.QuantityOwned > 0)
            .Select(uc => new
            {
                Game = uc.CardPrinting.Card.Game,
                PrintingId = uc.CardPrintingId,
                Qty = uc.QuantityOwned
            })
            .ToListAsync();

        long total = 0;
        var perGame = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in data)
        {
            if (!latest.TryGetValue(row.PrintingId, out var price)) continue;
            var add = price * row.Qty;
            total += add;
            perGame[row.Game] = perGame.TryGetValue(row.Game, out var g) ? g + add : add;
        }

        var slices = perGame.Select(kv => new GameSliceResponse(kv.Key, kv.Value));
        return Ok(new CollectionSummaryResponse(total, slices));
    }

    [HttpGet("deck/{deckId:int}")]
    public async Task<ActionResult<DeckSummaryResponse>> GetDeckValue(int deckId)
    {
        var deck = await _db.Decks.FindAsync(deckId);
        if (deck == null) return NotFound();

        var latest = await LatestPricesAsync(ValueScopeType.CardPrinting);

        var rows = await _db.DeckCards
            .Where(dc => dc.DeckId == deckId && dc.QuantityInDeck > 0)
            .Select(dc => new { dc.CardPrintingId, dc.QuantityInDeck })
            .ToListAsync();

        long total = 0;
        foreach (var r in rows)
            if (latest.TryGetValue(r.CardPrintingId, out var price))
                total += price * r.QuantityInDeck;

        return Ok(new DeckSummaryResponse(deckId, total));
    }

    private async Task<Dictionary<int, long>> LatestPricesAsync(ValueScopeType scope)
    {
        return await _db.ValueHistories
            .Where(v => v.ScopeType == scope)
            .GroupBy(v => v.ScopeId)
            .Select(g => g.OrderByDescending(v => v.AsOfUtc).First())
            .ToDictionaryAsync(v => v.ScopeId, v => v.PriceCents);
    }
}
