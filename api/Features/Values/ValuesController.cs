using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using api.Common.Errors;
using api.Filters;
using api.Data;
using api.Features.Values.Dtos;
using api.Authentication;
using api.Features.Decks;
using api.Models;
using Microsoft.AspNetCore.Http;
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
    [RequireUserHeader] // caller must identify as a user
    public async Task<IActionResult> Refresh([FromQuery] string game, [FromBody] List<RefreshItemRequest> items)
    {
        // Admin gate
        var me = HttpContext.GetCurrentUser();
        if (me is null || !me.IsAdmin)
            return Forbid();

        // ---- START existing refresh logic ----
        if (string.IsNullOrWhiteSpace(game))
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["game"] = new[] { "The 'game' query parameter is required." }
                },
                detail: "The refresh request must specify a game.");
        }

        if (items is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["items"] = new[] { "The request body must include an array of items." }
                },
                detail: "The refresh request body is missing.");
        }

        if (items.Count == 0)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["items"] = new[] { "At least one item must be provided." }
                },
                detail: "The refresh request must include at least one item.");
        }

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
        _ = inserted;
        _ = ignored;
        // ---- END existing refresh logic ----

        return NoContent();
    }

    [HttpGet("cardprinting/{id:int}")]
    [RequireUserHeader]
    public async Task<ActionResult<SeriesResponse>> GetCardPrintingSeries(int id)
    {
        var exists = await _db.CardPrintings.AnyAsync(x => x.Id == id);
        if (!exists)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing with id {id} was not found.");
        }

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
        if (user == null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["X-User-Id"] = new[] { "The X-User-Id header is required." }
                },
                detail: "The request must include a user identifier.");
        }

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
    [RequireUserHeader]
    public async Task<ActionResult<DeckSummaryResponse>> GetDeckValue(int deckId)
    {
        var currentUser = HttpContext.GetCurrentUser();

        var deck = await _db.Decks.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deckId);
        if (deck is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Deck with id {deckId} was not found.");
        }

        if (!DeckAuthorization.OwnsDeckOrAdmin(currentUser, deck.UserId))
        {
            return Forbid();
        }

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
