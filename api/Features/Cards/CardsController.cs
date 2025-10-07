using api.Data;
using api.Features.Cards.Dtos;
using api.Filters;
using api.Middleware;
using api.Models;
using api.Shared;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace api.Features.Cards;

[ApiController]
[RequireUserHeader]
[Route("api/cards")] // plural route
public class CardsController : ControllerBase
{
    private const string PlaceholderCardImage = "/images/placeholders/card-3x4.png";

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public CardsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // GET /api/cards?q=&game=Magic,Lorcana&skip=0&take=60&includeTotal=false
    [HttpGet]
    public async Task<ActionResult<CardListPageResponse>> ListCardsVirtualized(
        [FromQuery] string? q,
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 60,
        [FromQuery] bool includeTotal = false,
        CancellationToken ct = default)
    {
        take = take <= 0 ? 60 : take;
        if (take > 200) take = 200;
        if (skip < 0) skip = 0;

        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);

        IQueryable<Card> query = _db.Cards.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{term}%") ||
                EF.Functions.Like(c.CardType, $"%{term}%"));
        }

        if (games.Count > 0)
        {
            query = query.Where(c => games.Contains(c.Game));
        }

        if (sets.Count > 0)
        {
            query = query.Where(c => c.Printings.Any(p => sets.Contains(p.Set)));
        }

        if (rarities.Count > 0)
        {
            query = query.Where(c => c.Printings.Any(p => rarities.Contains(p.Rarity)));
        }

        query = query.OrderBy(c => c.Game).ThenBy(c => c.Name).ThenBy(c => c.CardId);

        int? total = null;
        if (includeTotal)
        {
            total = await query.CountAsync(ct);
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new CardListItemResponse
            {
                CardId = c.CardId,
                Game = c.Game,
                Name = c.Name,
                CardType = c.CardType,
                PrintingsCount = c.Printings.Count(),
                Primary = c.Printings
                    .OrderByDescending(p => !string.IsNullOrEmpty(p.ImageUrl))
                    .ThenByDescending(p => p.Style == "Standard")
                    .ThenBy(p => p.Set)
                    .ThenBy(p => p.Number)
                    .ThenBy(p => p.Id)
                    .Select(p => new CardListItemResponse.PrimaryPrintingResponse
                    {
                        Id = p.Id,
                        Set = p.Set,
                        Number = p.Number,
                        Rarity = p.Rarity,
                        Style = p.Style,
                        ImageUrl = string.IsNullOrEmpty(p.ImageUrl) ? PlaceholderCardImage : p.ImageUrl
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var nextSkip = items.Count < take ? (int?)null : skip + take;

        return Ok(new CardListPageResponse
        {
            Items = items,
            Total = total,
            NextSkip = nextSkip
        });
    }

    // CSV parsing logic moved to CsvUtils in api.Shared
    private bool NotAdmin()
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || !me.IsAdmin;
    }

    private async Task<IActionResult> ListCardsCore(
        string? game, string? name,
        bool includePrintings,
        int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 50;

        var q = _db.Cards.AsNoTracking().AsQueryable();
        var ct = HttpContext.RequestAborted;

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

        var total = await q.CountAsync(ct);

        q = q.OrderBy(c => c.Name).ThenBy(c => c.CardId);

        var cards = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (!includePrintings)
        {
            var rows = _mapper.Map<List<CardListItemResponse>>(cards);
            return Ok(new Paged<CardListItemResponse>(rows, total, page, pageSize));
        }

        var cardIds = cards.Select(c => c.CardId).ToList();

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cardIds.Contains(cp.CardId))
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync(ct);

        var map = printings
            .GroupBy(p => p.CardId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var detailed = cards.Select(c => new CardDetailResponse(
            c.CardId,
            c.Name,
            c.Game,
            c.CardType,
            c.Description,
            map.TryGetValue(c.CardId, out var list)
                ? _mapper.Map<List<CardPrintingResponse>>(list)
                : new List<CardPrintingResponse>()
        )).ToList();

        return Ok(new Paged<CardDetailResponse>(detailed, total, page, pageSize));
    }

    private async Task<IActionResult> GetCardCore(int cardId)
    {
        var c = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(x => x.CardId == cardId);
        if (c is null) return NotFound();

        var printings = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .OrderBy(cp => cp.Set).ThenBy(cp => cp.Number)
            .ToListAsync();

        var dto = new CardDetailResponse(
            c.CardId,
            c.Name,
            c.Game,
            c.CardType,
            c.Description,
            _mapper.Map<List<CardPrintingResponse>>(printings)
        );

        return Ok(dto);
    }

    private async Task<IActionResult> UpsertPrintingCore(UpsertPrintingRequest dto)
    {
        if (NotAdmin()) return StatusCode(403, "Admin required.");
        if (dto is null) return BadRequest();
        if (dto.CardId <= 0) return BadRequest("CardId required.");
        if (await _db.Cards.FindAsync(dto.CardId) is null) return NotFound("Card not found.");

        string? set = dto.Set?.Trim();
        string? number = dto.Number?.Trim();
        string? style = dto.Style?.Trim();

        CardPrinting? cp = null;

        if (dto.Id.HasValue)
        {
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x => x.Id == dto.Id.Value);
            if (cp is null) return NotFound("Printing not found by Id.");
        }
        else
        {
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
            cp = new CardPrinting
            {
                CardId = dto.CardId,
                Set = set!,
                Number = number!,
                Rarity = dto.Rarity?.Trim() ?? "",
                Style = style ?? "",
                ImageUrl = dto.ImageUrl
            };
            _db.CardPrintings.Add(cp);
        }
        else
        {
            if (dto.Set is not null) cp.Set = set!;
            if (dto.Number is not null) cp.Number = number!;
            if (dto.Rarity is not null) cp.Rarity = dto.Rarity.Trim();
            if (dto.Style is not null) cp.Style = style ?? "";
            if (dto.ImageUrlSet) cp.ImageUrl = dto.ImageUrl;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

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
                    Set = set!,
                    Number = number!,
                    Rarity = dto.Rarity?.Trim() ?? "",
                    Style = style ?? "",
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

    // Endpoints

    [HttpGet("search")]
    public async Task<IActionResult> ListCards(
        [FromQuery] string? game,
        [FromQuery] string? name,
        [FromQuery] bool includePrintings = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => await ListCardsCore(game, name, includePrintings, page, pageSize);

    [HttpGet("{cardId:int}")]
    public async Task<IActionResult> GetCard(int cardId)
        => await GetCardCore(cardId);

    [HttpGet("{cardId:int}/printings")]
    public async Task<ActionResult<IReadOnlyList<PrintingDto>>> GetCardPrintings(int cardId)
    {
        var exists = await _db.Cards.AnyAsync(c => c.CardId == cardId);
        if (!exists) return NotFound();

        var rows = await _db.CardPrintings
            .AsNoTracking()
            .Where(cp => cp.CardId == cardId)
            .OrderBy(cp => cp.Set)
            .ThenBy(cp => cp.Number)
            .Select(cp => new PrintingDto(
                cp.Id,
                cp.Set,
                null,
                cp.Number,
                cp.Rarity,
                cp.ImageUrl ?? PlaceholderCardImage))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("printing")]
    public async Task<IActionResult> UpsertPrinting([FromBody] UpsertPrintingRequest dto)
        => await UpsertPrintingCore(dto);

    [HttpPost("{cardId:int}/printings/import")]
    public async Task<IActionResult> BulkImportPrintings(int cardId, [FromBody] IEnumerable<UpsertPrintingRequest> items)
        => await BulkImportPrintingsCore(cardId, items);
}
