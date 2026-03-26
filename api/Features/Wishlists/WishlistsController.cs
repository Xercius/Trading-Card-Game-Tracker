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
[Route("api/wishlist")]
public class WishlistsController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;
    private const int UserId = DbSeeder.DefaultUserId;

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public WishlistsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
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

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.UserId == UserId && uc.QuantityWanted > 0)
            .AsNoTracking()
            .FilterByPrintingMetadata(game, set, rarity, name, cardPrintingId, useCaseInsensitiveName: true);

        var total = await query.CountAsync(ct);

        query = query.OrderByCardNameAndPrinting();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<WishlistItemDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

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

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            uc = new UserCard
            {
                UserId = UserId,
                CardPrintingId = dto.CardPrintingId,
                QuantityOwned = 0,
                QuantityProxyOwned = 0,
                QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted)
            };
            _db.UserCards.Add(uc);
        }
        else
        {
            uc.QuantityWanted = QuantityGuards.Clamp(dto.QuantityWanted);
        }

        await _db.SaveChangesAsync();
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

        var ids = list.Select(i => i.CardPrintingId).Distinct().ToList();
        var validIds = await _db.CardPrintings.Where(cp => ids.Contains(cp.Id)).Select(cp => cp.Id).ToListAsync();
        var missing = ids.FirstOrDefault(id => !validIds.Contains(id));
        if (missing != 0)
        {
            return this.CreateValidationProblem(
                "cardPrintingId",
                $"CardPrinting not found: {missing}");
        }

        var map = await _db.UserCards
            .Where(uc => uc.UserId == UserId && ids.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        foreach (var i in list)
        {
            if (!map.TryGetValue(i.CardPrintingId, out var uc))
            {
                uc = new UserCard
                {
                    UserId = UserId,
                    CardPrintingId = i.CardPrintingId,
                    QuantityOwned = 0,
                    QuantityProxyOwned = 0,
                    QuantityWanted = 0
                };
                _db.UserCards.Add(uc);
                map[i.CardPrintingId] = uc;
            }

            uc.QuantityWanted = QuantityGuards.Clamp(i.QuantityWanted);
        }

        await _db.SaveChangesAsync();
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

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            var initialMoveQuantity = dto.Quantity;
            var newUserCard = new UserCard
            {
                UserId = UserId,
                CardPrintingId = dto.CardPrintingId,
                QuantityWanted = 0,
                QuantityOwned = dto.UseProxy ? 0 : initialMoveQuantity,
                QuantityProxyOwned = dto.UseProxy ? initialMoveQuantity : 0
            };
            _db.UserCards.Add(newUserCard);
            await _db.SaveChangesAsync();

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

        var desiredQuantity = QuantityGuards.Clamp(uc.QuantityWanted);
        var moveQuantity = Math.Min(dto.Quantity, desiredQuantity);

        if (moveQuantity > 0)
        {
            uc.QuantityWanted = QuantityGuards.ClampDelta(uc.QuantityWanted, -moveQuantity);

            if (dto.UseProxy)
            {
                uc.QuantityProxyOwned = QuantityGuards.ClampDelta(uc.QuantityProxyOwned, moveQuantity);
            }
            else
            {
                uc.QuantityOwned = QuantityGuards.ClampDelta(uc.QuantityOwned, moveQuantity);
            }

            await _db.SaveChangesAsync();
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

    private async Task<IActionResult> RemoveCore(int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {cardPrintingId} was not found in your wishlist.");
        }

        uc.QuantityWanted = 0;

        if (uc.QuantityOwned == 0 && uc.QuantityProxyOwned == 0 && uc.QuantityWanted == 0)
            _db.UserCards.Remove(uc);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------------------
    // Routes
    // -----------------------------------------

    [HttpGet]
    public async Task<ActionResult<List<WishlistItemDto>>> GetAll(
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
        return Ok(pageResult!.Items.ToList());
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

        var card = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.CardPrintingId == dto.PrintingId);

        if (card is null)
        {
            card = new UserCard
            {
                UserId = UserId,
                CardPrintingId = dto.PrintingId,
                QuantityOwned = 0,
                QuantityProxyOwned = 0,
                QuantityWanted = QuantityGuards.Clamp(dto.Quantity)
            };
            _db.UserCards.Add(card);
        }
        else
        {
            card.QuantityWanted = QuantityGuards.ClampDelta(card.QuantityWanted, dto.Quantity);
        }

        await _db.SaveChangesAsync();
        return Ok(new QuickAddResponse(dto.PrintingId, card.QuantityWanted));
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
    public async Task<IActionResult> Remove(int cardPrintingId)
    {
        return await RemoveCore(cardPrintingId);
    }
}
