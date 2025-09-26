using AutoMapper;
using api.Common.Dtos;
using api.Data;
using api.Features.Cards.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

[ApiController]
[RequireUserHeader] // remove this if you want /api/card to be public
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
    // Core (single source of logic)
    // -----------------------------

    // GET list of unique cards (optionally filter + paginate)
    private async Task<IActionResult> ListCardsCore(
        string? game, string? name,
        bool includePrintings,
        int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 50;

        var q = _db.Cards.AsQueryable();

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
        var c = await _db.Cards.FirstOrDefaultAsync(x => x.Id == cardId);
        if (c is null) return NotFound();

        var printings = await _db.CardPrintings
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

    // Admin: upsert/patch a single printing (minimal example)
    private async Task<IActionResult> UpsertPrintingCore(UpsertPrintingRequest dto)
    {
        if (NotAdmin()) return StatusCode(403, "Admin required.");
        if (dto is null) return BadRequest();
        if (dto.CardId <= 0) return BadRequest("CardId required.");
        if (await _db.Cards.FindAsync(dto.CardId) is null) return NotFound("Card not found.");

        CardPrinting? cp = null;
        if (dto.Id.HasValue)
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x => x.Id == dto.Id.Value);

        if (cp is null)
        {
            cp = new CardPrinting
            {
                CardId = dto.CardId,
                Set = dto.Set?.Trim(),
                Number = dto.Number?.Trim(),
                Rarity = dto.Rarity?.Trim(),
                Style = dto.Style?.Trim(),
                ImageUrl = dto.ImageUrl
            };
            _db.CardPrintings.Add(cp);
        }
        else
        {
            if (dto.Set is not null) cp.Set = dto.Set.Trim();
            if (dto.Number is not null) cp.Number = dto.Number.Trim();
            if (dto.Rarity is not null) cp.Rarity = dto.Rarity.Trim();
            if (dto.Style is not null) cp.Style = dto.Style.Trim();
            if (dto.ImageUrlSet) cp.ImageUrl = dto.ImageUrl; // only set when caller intends to
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Admin: bulk import printings for a card (idempotent-ish by Set+Number+Style)
    private async Task<IActionResult> BulkImportPrintingsCore(int cardId, IEnumerable<UpsertPrintingRequest> items)
    {
        if (NotAdmin()) return StatusCode(403, "Admin required.");
        if (cardId <= 0) return BadRequest("CardId required.");
        if (items is null) return BadRequest("Payload required.");
        if (await _db.Cards.FindAsync(cardId) is null) return NotFound("Card not found.");

        var list = items.ToList();
        if (list.Count == 0) return NoContent();

        // Load existing for match-up
        var existing = await _db.CardPrintings
            .Where(cp => cp.CardId == cardId)
            .ToListAsync();

        foreach (var dto in list)
        {
            if (string.IsNullOrWhiteSpace(dto.Set) || string.IsNullOrWhiteSpace(dto.Number))
                continue;

            var match = existing.FirstOrDefault(x =>
                x.Set == dto.Set!.Trim() &&
                x.Number == dto.Number!.Trim() &&
                (x.Style ?? "") == (dto.Style ?? "").Trim()
            );

            if (match is null)
            {
                _db.CardPrintings.Add(new CardPrinting
                {
                    CardId = cardId,
                    Set = dto.Set!.Trim(),
                    Number = dto.Number!.Trim(),
                    Rarity = dto.Rarity?.Trim(),
                    Style = dto.Style?.Trim(),
                    ImageUrl = dto.ImageUrl
                });
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
    // Public endpoints (thin wrappers)
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

    // -----------------------------
    // Admin endpoints
    // -----------------------------

    // POST /api/card/printing    (create or update a single printing)
    [HttpPost("printing")]
    public async Task<IActionResult> UpsertPrinting([FromBody] UpsertPrintingRequest dto)
        => await UpsertPrintingCore(dto);

    // POST /api/card/{cardId}/printings/import   (bulk import/merge)
    [HttpPost("{cardId:int}/printings/import")]
    public async Task<IActionResult> BulkImportPrintings(int cardId, [FromBody] IEnumerable<UpsertPrintingRequest> items)
        => await BulkImportPrintingsCore(cardId, items);
}
