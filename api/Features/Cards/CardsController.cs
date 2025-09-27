using api.Common.Dtos;
using api.Data;
using api.Features.Cards.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace api.Features.Cards;

[ApiController]
[RequireUserHeader]
[Route("api/card")]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CardsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private bool NotAdmin()
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || !me.IsAdmin;
    }

    // -----------------------------
    // Core
    // -----------------------------

    // GET list of unique cards
    private async Task<IActionResult> ListCardsCore(
        string? game, string? name,
        bool includePrintings,
        int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 50;

        var q = _db.Cards.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(game))
        {
            var g = game.Trim();
            q = q.Where(c => c.Game == g);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(n));
        }

        var total = await q.CountAsync();

        var cards = await q
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (!includePrintings)
        {
            var rows = _mapper.Map<List<CardListItemResponse>>(cards);
            return Ok(new PagedResult<CardListItemResponse>(rows, total, page, pageSize));
        }

        var cardIds = cards.Select(c => c.Id).ToList();
        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cardIds.Contains(cp.CardId))
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync();

        var map = printings.GroupBy(p => p.CardId).ToDictionary(g => g.Key, g => g.ToList());

        var detailed = cards.Select(c => new CardDetailResponse(
            c.Id,
            c.Name,
            c.Game,
            map.TryGetValue(c.Id, out var list)
                ? _mapper.Map<List<CardPrintingResponse>>(list)
                : new List<CardPrintingResponse>())).ToList();

        return Ok(new PagedResult<CardDetailResponse>(detailed, total, page, pageSize));
    }

    // GET a single card + all printings
    private async Task<IActionResult> GetCardCore(int cardId)
    {
        var c = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cardId);
        if (c is null) return NotFound();

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync();

        var dto = new CardDetailResponse(
            c.Id,
            c.Name,
            c.Game,
            _mapper.Map<List<CardPrintingResponse>>(printings)
        );

        return Ok(dto);
    }

    // Admin: upsert/patch a single printing with natural-key dedupe
    private async Task<IActionResult> UpsertPrintingCore(UpsertPrintingRequest dto)
    {
        if (NotAdmin()) return StatusCode(403, "Admin required.");
        if (dto is null) return BadRequest();
        if (dto.CardId <= 0) return BadRequest("CardId required.");
        if (await _db.Cards.FindAsync(dto.CardId) is null) return NotFound("Card not found.");

        // normalize inputs
        string? set = dto.Set?.Trim();
        string? number = dto.Number?.Trim();
        string? style = dto.Style?.Trim();

        CardPrinting? cp = null;

        if (dto.Id.HasValue)
        {
            // explicit id path
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x => x.Id == dto.Id.Value);
            if (cp is null) return NotFound("Printing not found by Id.");
        }
        else
        {
            // natural key path: prevent duplicates
            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
                return BadRequest("Set and Number required when Id is omitted.");

            cp = await _db.CardPrintings.FirstOrDefaultAsync(x =>
                x.CardId == dto.CardId &&
                x.Set == set &&
                x.Number == number &&
                (x.Style ?? "") == (style ?? "")
            );
        }

        if (cp is null)
        {
            // create new
            cp = new CardPrinting
            {
                CardId = dto.CardId,
                Set = set,
                Number = number,
                Rarity = dto.Rarity?.Trim(),
                Style = style,
                ImageUrl = dto.ImageUrl
            };
            _db.CardPrintings.Add(cp);
        }
        else
        {
            // update existing
            if (dto.Set is not null) cp.Set = set!;
            if (dto.Number is not null) cp.Number = number!;
            if (dto.Rarity is not null) cp.Rarity = dto.Rarity.Trim();
            if (dto.Style is not null) cp.Style = style;
            if (dto.ImageUrlSet) cp.ImageUrl = dto.ImageUrl;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Admin: bulk import printings for a card (idempotent by Set+Number+Style)
    private async Task<IActionResult> BulkImportPrintingsCore(int cardId, IEnumerable<UpsertPrintingRequest> items)
    {
        if (NotAdmin()) return StatusCode(403, "Admin required.");
        if (cardId <= 0) return BadRequest("CardId required.");
        if (items is null) return BadRequest("Payload required.");
        if (await _db.Cards.FindAsync(cardId) is null) return NotFound("Card not found.");

        var list = items.ToList();
        if (list.Count == 0) return NoContent();

        var existing = await _db.CardPrintings
            .Where(cp => cp.CardId == cardId)
            .ToListAsync();

        foreach (var dto in list)
        {
            var set = dto.Set?.Trim();
            var number = dto.Number?.Trim();
            var style = dto.Style?.Trim();

            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
                continue;

            var match = existing.FirstOrDefault(x =>
                x.Set == set &&
                x.Number == number &&
                (x.Style ?? "") == (style ?? "")
            );

            if (match is null)
            {
                var created = new CardPrinting
                {
                    CardId = cardId,
                    Set = set,
                    Number = number,
                    Rarity = dto.Rarity?.Trim(),
                    Style = style,
                    ImageUrl = dto.ImageUrl
                };
                _db.CardPrintings.Add(created);
                existing.Add(created);
            }
            else
            {
                if (dto.Rarity is not null) match.Rarity = dto.Rarity.Trim();
                if (dto.ImageUrlSet) match.ImageUrl = dto.ImageUrl;
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------
    // Endpoints
    // -----------------------------

    // GET /api/card?game=&name=&includePrintings=false&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> ListCards(
        [FromQuery] string? game,
        [FromQuery] string? name,
        [FromQuery] bool includePrintings = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => await ListCardsCore(game, name, includePrintings, page, pageSize);

    // GET /api/card/{cardId}
    [HttpGet("{cardId:int}")]
    public async Task<IActionResult> GetCard(int cardId)
        => await GetCardCore(cardId);

    // POST /api/card/printing
    [HttpPost("printing")]
    public async Task<IActionResult> UpsertPrinting([FromBody] UpsertPrintingRequest dto)
        => await UpsertPrintingCore(dto);

    // POST /api/card/{cardId}/printings/import
    [HttpPost("{cardId:int}/printings/import")]
    public async Task<IActionResult> BulkImportPrintings(int cardId, [FromBody] IEnumerable<UpsertPrintingRequest> items)
        => await BulkImportPrintingsCore(cardId, items);
}
