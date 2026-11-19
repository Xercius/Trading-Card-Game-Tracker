using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Wishlists.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using WishlistItemDto = api.Features.Wishlists.Dtos.WishlistItemResponse;

namespace api.Features.Wishlists;

[ApiController]
[Route("api/wishlists")]
[Route("api/wishlist")]
public class WishlistsController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly WishlistSourceSyncService _sourceSync;

    public WishlistsController(AppDbContext db, IMapper mapper, WishlistSourceSyncService sourceSync)
    {
        _db = db;
        _mapper = mapper;
        _sourceSync = sourceSync;
    }

    // -----------------------------
    // Core (single source of logic)
    // -----------------------------

    private async Task<(Paged<WishlistItemDto>? Page, ActionResult? Error)> GetAllCore(
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        int page,
        int pageSize)
    {
        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.QuantityWanted > 0)
            .AsNoTracking()
            .FilterByPrintingMetadata(game, set, rarity, name, cardPrintingId, useCaseInsensitiveName: true);

        var total = await query.CountAsync(ct);

        query = query.OrderByCardNameAndPrinting();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<WishlistItemDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        if (items.Count > 0)
        {
            var printingIds = items.Select(i => i.CardPrintingId).ToList();
            var rawSources = await _db.WishlistSources
                .AsNoTracking()
                .Where(w => printingIds.Contains(w.CardPrintingId) && w.Quantity > 0)
                .Select(w => new
                {
                    w.CardPrintingId,
                    w.DeckId,
                    DeckName = w.Deck != null ? w.Deck.Name : "General",
                    w.Quantity
                })
                .ToListAsync(ct);

            var sourcesByPrinting = rawSources
                .GroupBy(s => s.CardPrintingId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<WishlistSourceDto>)g
                        .OrderBy(s => s.DeckId.HasValue ? 1 : 0)
                        .ThenBy(s => s.DeckName, StringComparer.OrdinalIgnoreCase)
                        .Select(s => new WishlistSourceDto(s.DeckId, s.DeckName, s.Quantity))
                        .ToList());

            items = items
                .Select(i => sourcesByPrinting.TryGetValue(i.CardPrintingId, out var s)
                    ? i with { Sources = s }
                    : i)
                .ToList();
        }

        return (new Paged<WishlistItemDto>(items, total, page, pageSize), null);
    }

    private async Task<IActionResult> UpsertCore(UpsertWishlistRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        if (dto.CardPrintingId <= 0)
        {
            return this.CreateValidationProblem("cardPrintingId", "CardPrintingId must be positive.");
        }

        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.CardPrintingId} was not found.");
        }

        if (dto.DeckId.HasValue && !await _db.Decks.AnyAsync(d => d.Id == dto.DeckId.Value, HttpContext.RequestAborted))
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Deck {dto.DeckId.Value} was not found.");
        }

        await _sourceSync.SetSourceQuantityAsync(dto.CardPrintingId, dto.DeckId, dto.QuantityWanted, HttpContext.RequestAborted);
        return NoContent();
    }

    private async Task<IActionResult> BulkSetCore(IEnumerable<BulkSetWishlistRequest> items)
    {
        if (items is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Payload required.");
        }

        var list = items.ToList();
        if (list.Count == 0) return NoContent();
        if (list.Any(i => i.CardPrintingId <= 0))
        {
            return this.CreateValidationProblem("cardPrintingId", "CardPrintingId must be positive.");
        }

        var ct = HttpContext.RequestAborted;
        var ids = list.Select(i => i.CardPrintingId).Distinct().ToList();
        var validIds = await _db.CardPrintings.Where(cp => ids.Contains(cp.Id)).Select(cp => cp.Id).ToListAsync(ct);
        var missing = ids.FirstOrDefault(id => !validIds.Contains(id));
        if (missing != 0)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"CardPrinting {missing} was not found.");
        }

        var deckIds = list.Where(i => i.DeckId.HasValue).Select(i => i.DeckId!.Value).Distinct().ToList();
        if (deckIds.Count > 0)
        {
            var validDeckIds = await _db.Decks.Where(d => deckIds.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);
            var missingDeck = deckIds.FirstOrDefault(id => !validDeckIds.Contains(id));
            if (missingDeck != 0)
            {
                return this.CreateProblem(
                    StatusCodes.Status404NotFound,
                    detail: $"Deck {missingDeck} was not found.");
            }
        }

        foreach (var i in list)
        {
            await _sourceSync.SetSourceQuantityAsync(i.CardPrintingId, i.DeckId, i.QuantityWanted, ct);
        }

        return NoContent();
    }

    private async Task<ActionResult<MoveToCollectionResponse>> MoveToCollectionCore(MoveToCollectionRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        var errors = new Dictionary<string, string[]>();
        if (dto.CardPrintingId <= 0)
        {
            errors["cardPrintingId"] = new[] { "CardPrintingId must be positive." };
        }

        if (dto.Quantity <= 0)
        {
            errors["quantity"] = new[] { "Quantity must be positive." };
        }

        if (errors.Count > 0)
        {
            return this.CreateValidationProblem(errors);
        }

        var ct = HttpContext.RequestAborted;
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.CardPrintingId == dto.CardPrintingId, ct);

        if (uc is null)
        {
            var initialMoveQuantity = dto.Quantity;
            var newUserCard = new UserCard
            {
                CardPrintingId = dto.CardPrintingId,
                QuantityWanted = 0,
                QuantityOwned = dto.UseProxy ? 0 : initialMoveQuantity,
                QuantityProxyOwned = dto.UseProxy ? initialMoveQuantity : 0
            };
            _db.UserCards.Add(newUserCard);
            await _db.SaveChangesAsync(ct);

            var (initialAvailability, initialAvailabilityWithProxies) = CardAvailabilityHelper.Calculate(
                newUserCard.QuantityOwned,
                newUserCard.QuantityProxyOwned);

            return Ok(new MoveToCollectionResponse(
                newUserCard.CardPrintingId,
                newUserCard.QuantityWanted,
                newUserCard.QuantityOwned,
                newUserCard.QuantityProxyOwned,
                initialAvailability,
                initialAvailabilityWithProxies));
        }

        var sources = await _db.WishlistSources
            .Where(w => w.CardPrintingId == dto.CardPrintingId && w.Quantity > 0)
            .ToListAsync(ct);

        if (sources.Count > 1 && !dto.SourceSpecified && dto.DeckId is null)
        {
            return this.CreateValidationProblem("deckId", "Specify which wishlist source to move when multiple sources exist.");
        }

        var targetSource = sources.Count == 1 && !dto.SourceSpecified && dto.DeckId is null
            ? sources[0]
            : sources.FirstOrDefault(s => s.DeckId == dto.DeckId);

        var availableWanted = targetSource?.Quantity ?? (sources.Count == 0 ? QuantityGuards.Clamp(uc.QuantityWanted) : 0);
        var moveQuantity = Math.Min(dto.Quantity, availableWanted);

        if (moveQuantity > 0)
        {
            if (dto.UseProxy)
            {
                uc.QuantityProxyOwned = QuantityGuards.ClampDelta(uc.QuantityProxyOwned, moveQuantity);
            }
            else
            {
                uc.QuantityOwned = QuantityGuards.ClampDelta(uc.QuantityOwned, moveQuantity);
            }

            if (targetSource is not null)
            {
                await _db.SaveChangesAsync(ct);
                uc = await _sourceSync.DeltaSourceQuantityAsync(dto.CardPrintingId, targetSource.DeckId, -moveQuantity, ct) ?? uc;
            }
            else
            {
                uc.QuantityWanted = QuantityGuards.ClampDelta(uc.QuantityWanted, -moveQuantity);
                await _db.SaveChangesAsync(ct);
            }
        }

        var (availability, availabilityWithProxies) = CardAvailabilityHelper.Calculate(
            uc.QuantityOwned,
            uc.QuantityProxyOwned);

        return Ok(new MoveToCollectionResponse(
            uc.CardPrintingId,
            uc.QuantityWanted,
            uc.QuantityOwned,
            uc.QuantityProxyOwned,
            availability,
            availabilityWithProxies));
    }

    private async Task<IActionResult> RemoveCore(int cardPrintingId, int? deckId = null, bool generalOnly = false)
    {
        var ct = HttpContext.RequestAborted;
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.CardPrintingId == cardPrintingId, ct);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {cardPrintingId} was not found in your wishlist.");
        }

        if (deckId.HasValue)
        {
            await _sourceSync.SetSourceQuantityAsync(cardPrintingId, deckId.Value, 0, ct);
        }
        else if (generalOnly)
        {
            await _sourceSync.SetSourceQuantityAsync(cardPrintingId, null, 0, ct);
        }
        else
        {
            await _sourceSync.RemoveAllSourcesForPrintingAsync(cardPrintingId, ct);
        }

        return NoContent();
    }

    // -----------------------------------------
    // Routes
    // -----------------------------------------

    [HttpGet]
    public async Task<ActionResult<Paged<WishlistItemDto>>> GetAll(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (pageResult, error) = await GetAllCore(game, set, rarity, name, cardPrintingId, page, pageSize);
        if (error is not null) return error;
        return Ok(pageResult);
    }

    [HttpPost("items")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<QuickAddResponse>> QuickAdd([FromBody] QuickAddRequest dto)
    {
        if (dto is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "A request body is required.");
        }

        var errors = new Dictionary<string, string[]>();
        if (dto.PrintingId <= 0)
        {
            errors["printingId"] = new[] { "printingId must be positive." };
        }

        if (dto.Quantity <= 0)
        {
            errors["quantity"] = new[] { "Quantity must be positive." };
        }

        if (errors.Count > 0)
        {
            return this.CreateValidationProblem(errors);
        }

        if (await _db.CardPrintings.FindAsync(dto.PrintingId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.PrintingId} was not found.");
        }

        if (dto.DeckId.HasValue && !await _db.Decks.AnyAsync(d => d.Id == dto.DeckId.Value, HttpContext.RequestAborted))
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Deck {dto.DeckId.Value} was not found.");
        }

        var card = await _sourceSync.DeltaSourceQuantityAsync(dto.PrintingId, dto.DeckId, dto.Quantity, HttpContext.RequestAborted);
        return Ok(new QuickAddResponse(dto.PrintingId, card?.QuantityWanted ?? 0));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertWishlistRequest dto)
    {
        return await UpsertCore(dto);
    }

    [HttpPut]
    public async Task<IActionResult> BulkSet([FromBody] IEnumerable<BulkSetWishlistRequest> items)
    {
        return await BulkSetCore(items);
    }

    [HttpPost("move-to-collection")]
    public async Task<ActionResult<MoveToCollectionResponse>> MoveToCollection([FromBody] MoveToCollectionRequest dto)
    {
        return await MoveToCollectionCore(dto);
    }

    [HttpDelete("{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(
        int cardPrintingId,
        [FromQuery] int? deckId = null,
        [FromQuery] bool generalOnly = false)
    {
        return await RemoveCore(cardPrintingId, deckId, generalOnly);
    }
}
