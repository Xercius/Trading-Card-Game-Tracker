using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Cards.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Cards;

[ApiController]
[Authorize]
[Route("api/cards")] // plural route
public class CardsController : ControllerBase
{
    private const string PlaceholderCardImage = "/images/placeholders/card-3x4.png";
    private const int VirtualizedDefaultTake = 60;
    private const int VirtualizedMaxTake = 200;
    private const int MinimumSkip = 0;
    private const int DefaultPageNumber = 1;
    private const int SearchDefaultPageSize = 50;
    private const int SearchMaxPageSize = 200;

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
        take = take <= 0 ? VirtualizedDefaultTake : Math.Min(take, VirtualizedMaxTake);
        if (skip < 0) skip = MinimumSkip;

        var games = CsvUtils.Parse(game);
        var sets = CsvUtils.Parse(set);
        var rarities = CsvUtils.Parse(rarity);

        var query = _db.Cards
            .AsNoTracking()
            .ApplyCardSearchFilters(q, games, sets, rarities)
            .OrderBy(c => c.Game)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.CardId);

        int? total = null;
        if (includeTotal)
        {
            total = await query.CountAsync(ct);
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .SelectCardSummaries(PlaceholderCardImage)
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
        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0 || pageSize > SearchMaxPageSize) pageSize = SearchDefaultPageSize;

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
        if (c is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card {cardId} was not found.");
        }

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
        if (NotAdmin())
        {
            return this.CreateProblem(StatusCodes.Status403Forbidden, detail: "Admin privileges are required to modify printings.");
        }

        if (dto is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "Printing payload is required." }
                });
        }

        if (dto.CardId <= 0)
        {
            return this.CreateValidationProblem(nameof(dto.CardId), "CardId must be provided.");
        }

        if (await _db.Cards.FindAsync(dto.CardId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {dto.CardId} was not found.");
        }

        string? set = dto.Set?.Trim();
        string? number = dto.Number?.Trim();
        string? style = dto.Style?.Trim();

        CardPrinting? cp = null;

        if (dto.Id.HasValue)
        {
            cp = await _db.CardPrintings.FirstOrDefaultAsync(x => x.Id == dto.Id.Value);
            if (cp is null)
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"Printing {dto.Id.Value} was not found.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number))
            {
                var errors = new Dictionary<string, string[]>();

                AddRequiredFieldError(errors, set, nameof(dto.Set), "Set is required when Id is omitted.");
                AddRequiredFieldError(errors, number, nameof(dto.Number), "Number is required when Id is omitted.");

                return this.CreateValidationProblem(errors);
            }

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
        if (NotAdmin())
        {
            return this.CreateProblem(StatusCodes.Status403Forbidden, detail: "Admin privileges are required to bulk import printings.");
        }

        if (cardId <= 0)
        {
            return this.CreateValidationProblem(nameof(cardId), "cardId must be provided.");
        }

        if (items is null)
        {
            return this.CreateValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = new[] { "Import payload is required." }
                });
        }

        if (await _db.Cards.FindAsync(cardId) is null)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {cardId} was not found.");
        }

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
        if (!exists)
        {
            return this.CreateProblem(StatusCodes.Status404NotFound, detail: $"Card {cardId} was not found.");
        }

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

    private static void AddRequiredFieldError(IDictionary<string, string[]> errors, string? value, string fieldName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[fieldName] = new[] { message };
        }
    }
}
