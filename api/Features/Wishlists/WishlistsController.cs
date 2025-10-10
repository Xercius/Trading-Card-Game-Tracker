using api.Authentication;
using api.Common.Errors;
using api.Data;
using api.Features._Common;
using api.Features.Wishlists.Dtos;
using api.Models;
using api.Shared;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using WishlistItemDto = api.Features.Wishlists.Dtos.WishlistItemResponse;

namespace api.Features.Wishlists;

[ApiController]
[Authorize]
[Route("api/user/{userId:int}/wishlist")]
// NOTE: Keep legacy {userId}-based routes for now; prefer /api/wishlist aliases below.
public class WishlistsController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 50;

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public WishlistsController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private bool UserMismatch(int userId)
    {
        var me = HttpContext.GetCurrentUser();
        return me is null || (!me.IsAdmin && me.Id != userId);
    }

    private bool TryResolveCurrentUserId(out int userId, out ActionResult? error)
    {
        var me = HttpContext.GetCurrentUser();
        if (me is null)
        {
            error = Forbid();
            userId = 0;
            return false;
        }
        error = null;
        userId = me.Id;
        return true;
    }


    // -----------------------------
    // Core (single source of logic)
    // -----------------------------

    // GET list (filters applied in DB)
    private async Task<(Paged<WishlistItemDto>? Page, ActionResult? Error)> GetAllCore(
        int userId,
        string? game,
        string? set,
        string? rarity,
        string? name,
        int? cardPrintingId,
        int page,
        int pageSize)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            return (null, this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {userId} was not found."));
        }

        if (page <= 0) page = DefaultPageNumber;
        if (pageSize <= 0) pageSize = DefaultPageSize;

        var ct = HttpContext.RequestAborted;

        var query = _db.UserCards
            .Where(uc => uc.UserId == userId && uc.QuantityWanted > 0)
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

    // POST upsert one (wanted)
    private async Task<IActionResult> UpsertCore(int userId, UpsertWishlistRequest dto)
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

        if (await _db.Users.FindAsync(userId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {userId} was not found.");
        }

        if (await _db.CardPrintings.FindAsync(dto.CardPrintingId) is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Card printing {dto.CardPrintingId} was not found.");
        }

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            uc = new UserCard
            {
                UserId = userId,
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

    // PUT bulk set (wanted)
    private async Task<IActionResult> BulkSetCore(int userId, IEnumerable<BulkSetWishlistRequest> items)
    {
        if (items is null)
        {
            return this.CreateProblem(
                StatusCodes.Status400BadRequest,
                title: "Invalid payload",
                detail: "Payload required.");
        }

        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {userId} was not found.");
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
            .Where(uc => uc.UserId == userId && ids.Contains(uc.CardPrintingId))
            .ToDictionaryAsync(uc => uc.CardPrintingId);

        foreach (var i in list)
        {
            if (!map.TryGetValue(i.CardPrintingId, out var uc))
            {
                uc = new UserCard
                {
                    UserId = userId,
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

    // POST move-to-collection (decrement wanted, increment owned/proxy)
    private async Task<ActionResult<MoveToCollectionResponse>> MoveToCollectionCore(int userId, MoveToCollectionRequest dto)
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

        if (!await _db.Users.AnyAsync(u => u.Id == userId))
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"User {userId} was not found.");
        }

        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == dto.CardPrintingId);

        if (uc is null)
        {
            var initialMoveQuantity = dto.Quantity;
            var newUserCard = new UserCard
            {
                UserId = userId,
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

    // DELETE (set wanted to 0; remove row if all counts are 0 after)
    private async Task<IActionResult> RemoveCore(int userId, int cardPrintingId)
    {
        var uc = await _db.UserCards
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CardPrintingId == cardPrintingId);
        if (uc is null)
        {
            return this.CreateProblem(
                StatusCodes.Status404NotFound,
                detail: $"Wishlist entry for user {userId} and card printing {cardPrintingId} was not found.");
        }

        uc.QuantityWanted = 0;

        // Optional clean-up: if fully zero, remove the row
        if (uc.QuantityOwned == 0 && uc.QuantityProxyOwned == 0 && uc.QuantityWanted == 0)
            _db.UserCards.Remove(uc);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -----------------------------------------
    // Legacy routes (userId in the URL)
    // -----------------------------------------

    [HttpGet]
    public async Task<ActionResult<Paged<WishlistItemDto>>> GetAll(
        int userId,
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        var (pageResult, error) = await GetAllCore(userId, game, set, rarity, name, cardPrintingId, page, pageSize);
        if (error is not null) return error;
        return pageResult!;
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(int userId, [FromBody] UpsertWishlistRequest dto)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await UpsertCore(userId, dto);
    }

    [HttpPut]
    public async Task<IActionResult> BulkSet(int userId, [FromBody] IEnumerable<BulkSetWishlistRequest> items)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await BulkSetCore(userId, items);
    }

    [HttpPost("move-to-collection")]
    public async Task<ActionResult<MoveToCollectionResponse>> MoveToCollection(int userId, [FromBody] MoveToCollectionRequest dto)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await MoveToCollectionCore(userId, dto);
    }

    [HttpDelete("{cardPrintingId:int}")]
    public async Task<IActionResult> Remove(int userId, int cardPrintingId)
    {
        if (UserMismatch(userId)) return StatusCode(403, "User mismatch.");
        return await RemoveCore(userId, cardPrintingId);
    }

    // -----------------------------------------
    // Auth-derived alias routes (/api/wishlist)
    // -----------------------------------------

    [HttpPost("/api/wishlist/items")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<QuickAddResponse>> QuickAddForCurrent([FromBody] QuickAddRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
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
            .FirstOrDefaultAsync(x => x.UserId == uid && x.CardPrintingId == dto.PrintingId);

        if (card is null)
        {
            card = new UserCard
            {
                UserId = uid,
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

    [HttpGet("/api/wishlist")]
    public async Task<ActionResult<List<WishlistItemDto>>> GetAllForCurrent(
        [FromQuery] string? game,
        [FromQuery] string? set,
        [FromQuery] string? rarity,
        [FromQuery] string? name,
        [FromQuery] int? cardPrintingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        var (pageResult, error) = await GetAllCore(uid, game, set, rarity, name, cardPrintingId, page, pageSize);
        if (error is not null) return error;
        return Ok(pageResult!.Items.ToList());
    }

    [HttpPost("/api/wishlist")]
    public async Task<IActionResult> UpsertForCurrent([FromBody] UpsertWishlistRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await UpsertCore(uid, dto);
    }

    [HttpPut("/api/wishlist")]
    public async Task<IActionResult> BulkSetForCurrent([FromBody] IEnumerable<BulkSetWishlistRequest> items)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await BulkSetCore(uid, items);
    }

    [HttpPost("/api/wishlist/move-to-collection")]
    public async Task<ActionResult<MoveToCollectionResponse>> MoveToCollectionForCurrent([FromBody] MoveToCollectionRequest dto)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await MoveToCollectionCore(uid, dto);
    }

    [HttpDelete("/api/wishlist/{cardPrintingId:int}")]
    public async Task<IActionResult> RemoveForCurrent(int cardPrintingId)
    {
        if (!TryResolveCurrentUserId(out var uid, out var err)) return err!;
        return await RemoveCore(uid, cardPrintingId);
    }
}
